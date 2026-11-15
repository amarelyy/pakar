from pathlib import Path

import numpy as np
import pytest

from occupancy import Detection
from roi_mapping import CameraROIConfig
from slot_labels import SlotOccupancyAdapter
from zone_occupancy import ZoneOccupancyTracker


def counts(occupied, capacity=2, unknown=0):
    return dict(areaId=1, capacity=capacity, occupied=occupied,
                available=capacity-occupied-unknown, unknown=unknown)


@pytest.mark.parametrize('occupied,status', [(0,'available'), (8,'available'),
                                                           (9,'almost_full'), (10,'full')])
def test_zone_status_matches_backend_rule(occupied, status):
    zone, initial = ZoneOccupancyTracker(1, 10).observe(counts(occupied, 10), 0)
    assert zone['status'] == status
    assert initial['kind'] == 'zone_snapshot'


def test_only_actual_complete_count_changes_emit_count_events():
    tracker = ZoneOccupancyTracker(1, 2)
    assert tracker.observe(counts(0), 0)[1]['kind'] == 'zone_snapshot'
    assert tracker.observe(counts(0), 1)[1] is None
    added = tracker.observe(counts(1), 2)[1]
    assert (added['kind'], added['previous_occupied'], added['zone']['occupied']) == ('zone_count_changed',0,1)
    assert tracker.observe(counts(1), 3)[1] is None
    removed = tracker.observe(counts(0), 4)[1]
    assert (removed['previous_occupied'], removed['zone']['occupied']) == (1,0)
    assert tracker.count_changes == 2


def test_unknown_never_becomes_empty_or_a_false_count_change():
    tracker = ZoneOccupancyTracker(1, 2)
    tracker.observe(counts(1), 0)
    partial, event = tracker.observe(counts(0, unknown=1), 1)
    assert not partial['complete'] and partial['status'] == 'unknown'
    assert (partial['occupiedMin'], partial['occupiedMax']) == (0,1)
    assert event is None
    assert tracker.observe(counts(1), 2)[1] is None
    tracker.invalidate()
    assert tracker.current['unknown'] == 2
    assert tracker.observe(counts(1), 10)[1] is None
    assert tracker.count_changes == 0


def test_first_partial_observation_has_no_fabricated_count_baseline():
    tracker = ZoneOccupancyTracker(1, 2)
    assert tracker.observe(counts(0, unknown=2), 0)[1] is None
    assert tracker.observe(counts(2), 1)[1]['kind'] == 'zone_snapshot'
    assert tracker.count_changes == 0


def test_recovery_compares_against_last_complete_count_not_partial_count():
    tracker = ZoneOccupancyTracker(1,2)
    tracker.observe(counts(2),0)
    assert tracker.observe(counts(0,unknown=2),1)[1] is None
    event = tracker.observe(counts(1),2)[1]
    assert event['previous_occupied'] == 2 and event['zone']['occupied'] == 1
    assert tracker.count_changes == 1


@pytest.mark.parametrize('capacity', [0,-1])
def test_zone_requires_configured_rois(capacity):
    with pytest.raises(ValueError,match='positive'):
        ZoneOccupancyTracker(1,capacity)


def test_slot_swap_keeps_both_slot_updates_without_a_zone_count_event():
    cfg = CameraROIConfig.load(Path(__file__).resolve().parents[2]/'edge/python-agent/configs/local-video.json')
    engine = SlotOccupancyAdapter(cfg, occupy_after=0, free_after=0)
    tracker = ZoneOccupancyTracker(cfg.area_id, len(cfg.spots))
    left, right = (85,120,265,350), (370,120,550,350)
    first = engine.update([Detection(left,.9,'occupied'),Detection(right,.9,'empty')],640,480,0)
    assert len(first) == 2
    tracker.observe(engine.zone_counts(), 0)
    swapped = [Detection(left,.9,'empty'),Detection(right,.9,'occupied')]
    changes = engine.update(swapped,640,480,1)
    assert {(e.spot_number,e.status) for e in changes} == {('H-01','available'),('H-02','occupied')}
    assert tracker.observe(engine.zone_counts(),1)[1] is None
    assert engine.update(swapped,640,480,2) == []


def test_detection_center_outside_roi_is_not_assigned_despite_overlap():
    cfg = CameraROIConfig.load(Path(__file__).resolve().parents[2]/'edge/python-agent/configs/local-video.json')
    engine = SlotOccupancyAdapter(cfg)
    changes = engine.update([Detection((0,120,100,350),.9,'occupied')],640,480,0)
    assert changes == [] and engine.zone_counts()['unknown'] == 2


def test_pending_exit_is_a_range_until_debounce_confirms_count_change():
    cfg = CameraROIConfig.load(Path(__file__).resolve().parents[2]/'edge/python-agent/configs/local-video.json')
    engine = SlotOccupancyAdapter(cfg)
    tracker = ZoneOccupancyTracker(1,2)
    def observe(left, t):
        changes = engine.update([Detection((85,120,265,350),.9,left),
                                 Detection((370,120,550,350),.9,'occupied')],640,480,t)
        return changes, *tracker.observe(engine.zone_counts(),t,engine.pending_counts())
    observe('occupied',0)
    changes, pending, event = observe('empty',1)
    assert changes == [] and event is None
    assert pending['pending'] == 1 and not pending['complete']
    assert pending['status'] == 'unknown'
    assert (pending['occupiedMin'],pending['occupiedMax']) == (1,2)
    assert observe('empty',3.9)[2] is None
    changes, settled, event = observe('empty',4)
    assert len(changes)==1 and settled['complete'] and settled['pending']==0
    assert event['previous_occupied']==2 and event['zone']['occupied']==1


def test_pending_entry_and_cancelled_candidate_do_not_publish_false_counts():
    tracker = ZoneOccupancyTracker(1,2)
    tracker.observe(counts(0),0)
    pending, event = tracker.observe(counts(0),1,{'toOccupied':1})
    assert (pending['occupiedMin'],pending['occupiedMax']) == (0,1) and event is None
    assert tracker.observe(counts(0),2)[1] is None
    assert tracker.count_changes == 0


@pytest.mark.parametrize('bad', ['duplicate_name','empty_name'])
def test_ambiguous_roi_names_cannot_silently_merge_two_slots(bad):
    cfg = CameraROIConfig.load(Path(__file__).resolve().parents[2]/'edge/python-agent/configs/local-video.json')
    cfg.spots[1].spot_number = cfg.spots[0].spot_number if bad == 'duplicate_name' else ''
    with pytest.raises(ValueError, match='spot_number'):
        cfg.validate()


def test_actual_yolo_postprocessing_filters_confidence_and_same_class_duplicates():
    import torch
    from ultralytics.utils.nms import non_max_suppression
    # Raw xywh + two class scores; seven boxes avoid the end-to-end Nx6 shortcut.
    prediction = torch.tensor([[[100,100,40,40,.9,.01],
                                [101,100,40,40,.8,.01],
                                [100,100,40,40,.01,.85],
                                [300,300,40,40,.05,.01],
                                [350,350,40,40,.01,.01],
                                [400,400,40,40,.01,.01],
                                [450,450,40,40,.01,.01]]], dtype=torch.float32).transpose(1,2)
    result = non_max_suppression(prediction, conf_thres=.1, iou_thres=.7,
                                 agnostic=False, nc=2, end2end=False)[0]
    assert result.shape == (2,6)
    assert sorted(result[:,5].tolist()) == [0.,1.]
    assert sorted(result[:,4].tolist()) == pytest.approx([.85,.9])


def test_detector_passes_explicit_class_aware_nms_configuration(tmp_path, monkeypatch):
    import ultralytics
    from pipeline import YOLODetector
    calls = []
    class Model:
        names = {0:'empty',1:'occupied'}
        def __init__(self, path):
            pass
        def predict(self, frame, **kwargs):
            calls.append(kwargs)
            return [type('Result',(),{'boxes':[]})()]
    monkeypatch.setattr(ultralytics, 'YOLO', Model)
    weights = tmp_path/'fixture.pt'
    weights.touch()
    assert YOLODetector(weights, nms_iou=.6)(np.zeros((10,10,3),np.uint8)) == []
    assert calls[0]['conf'] == .1 and calls[0]['iou'] == .6
    assert calls[0]['agnostic_nms'] is False


def test_pipeline_preserves_spot_payloads_and_separates_zone_count_events(tmp_path):
    import json
    from pipeline import parse_args, run_pipeline
    from test_agent import write_clip
    video = tmp_path/'transitions.mp4'
    write_clip(video,[np.full((480,640,3),80+i,np.uint8) for i in range(5)])
    sequence = [('empty','empty'),('occupied','empty'),('empty','occupied'),
                ('empty','occupied'),('empty','empty')]
    class Detector:
        kind = 'slot_schedule_fixture'
        names = {0:'empty',1:'occupied'}
        index = 0
        def __call__(self, frame):
            left, right = sequence[self.index]
            self.index += 1
            return [Detection((85,120,265,350),.9,left), Detection((370,120,550,350),.9,right)]
    class Client:
        def __init__(self):
            self.payloads = []
        def send(self, payload):
            self.payloads.append(payload)
            return True
    journal = tmp_path/'events.jsonl'
    args = parse_args(['--source',str(video),'--roi',str(Path(__file__).resolve().parents[2]/'edge/python-agent/configs/local-video.json'),
                       '--occupy-after','0','--free-after','0','--heartbeat','100','--events',str(journal)])
    client = Client()
    summary = run_pipeline(args,detector=Detector(),client=client)
    records = [json.loads(line) for line in journal.read_text().splitlines()]
    zone_events = [r for r in records if r['kind']=='zone_count_changed']
    assert [(r['media_seconds'],r['previous_occupied'],r['zone']['occupied']) for r in zone_events] == [(.5,0,1),(2.,1,0)]
    assert summary['zone_count_change_events'] == 2
    assert summary['final_zone_status']['status'] == 'available'
    assert [p['type'] for p in client.payloads] == ['heartbeat','change','change','change']
    assert {(s['spotNumber'],s['status']) for s in client.payloads[2]['spots']} == {('H-01','available'),('H-02','occupied')}
    assert all(set(p)=={'cameraId','areaId','type','timestamp','spots'} for p in client.payloads)
