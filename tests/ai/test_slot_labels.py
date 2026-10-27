from pathlib import Path
import pytest

from occupancy import Detection
from roi_mapping import CameraROIConfig
from slot_labels import SlotLabelEngine, validate_slot_classes


def config():
    return CameraROIConfig.load(Path(__file__).resolve().parents[2] / 'edge/python-agent/configs/local-video.json')


def box(label, conf=.9):
    return Detection((85,120,265,350), conf, label)


def test_slot_model_contract_rejects_vehicle_and_classifier():
    validate_slot_classes({0:'empty',1:'occupied'})
    with pytest.raises(ValueError):
        validate_slot_classes({0:'car'})
    with pytest.raises(ValueError):
        validate_slot_classes({0:'empty',1:'occupied'}, 'classify')


def test_absent_slot_does_not_become_available_and_gap_resets_debounce():
    engine = SlotLabelEngine(config())
    assert engine.update([box('occupied')],640,480,0)['states'] == {'H-01':'occupied'}
    assert engine.update([box('empty')],640,480,1)['states']['H-01'] == 'occupied'
    assert 'H-01' not in engine.update([],640,480,3)['states']
    assert engine.update([box('empty')],640,480,4)['states']['H-01'] == 'occupied'
    assert engine.update([box('empty')],640,480,7)['states']['H-01'] == 'available'


def test_conflicting_labels_are_unknown_and_one_box_matches_one_slot():
    engine = SlotLabelEngine(config())
    result = engine.update([box('occupied',.8),box('empty',.78)],640,480,0)
    assert result['unknown']['H-01'] == 'conflicting_labels'
    assert result['states'] == {}
    result = engine.update([box('empty',.95),box('occupied',.4)],640,480,1)
    assert result['states'] == {'H-01':'available'}


def test_low_confidence_and_nan_are_not_evidence_of_empty_slot():
    engine = SlotLabelEngine(config())
    result = engine.update([box('empty',.1),box('occupied',float('nan'))],640,480,0)
    assert not result['states']


def test_occupied_requires_two_seconds_after_reset_pending():
    engine = SlotLabelEngine(config())
    assert engine.update([box('empty')],640,480,0)['states']['H-01'] == 'available'
    assert engine.update([box('occupied')],640,480,1)['states']['H-01'] == 'available'
    engine.reset_pending()
    assert engine.update([box('occupied')],640,480,3)['states']['H-01'] == 'available'
    assert engine.update([box('occupied')],640,480,4.9)['states']['H-01'] == 'available'
    assert engine.update([box('occupied')],640,480,5)['states']['H-01'] == 'occupied'


def test_pipeline_custom_defaults_and_partial_payload(tmp_path):
    import numpy as np
    from test_agent import write_clip
    from pipeline import parse_args, run_pipeline
    video = tmp_path/'slots.mp4'
    write_clip(video,[np.full((480,640,3),80+i,np.uint8) for i in range(4)])
    class Detector:
        kind = 'slot_fixture'
        names = {0:'empty',1:'occupied'}
        def __call__(self, frame):
            return [box('occupied')]
    class Client:
        payloads = []
        def send(self, payload):
            self.payloads.append(payload)
            return True
    args = parse_args(['--source',str(video),'--roi',str(Path(__file__).resolve().parents[2]/'edge/python-agent/configs/local-video.json'),
                       '--annotated-video',str(tmp_path/'overlay.mp4')])
    assert Path(args.model).name == 'best.pt'
    client = Client()
    summary = run_pipeline(args,detector=Detector(),client=client)
    assert summary['mode'] == 'slots' and summary['min_conf'] == .25
    assert summary['final_zone'] == dict(areaId=1,capacity=2,occupied=1,available=0,unknown=1)
    assert [s['spotId'] for s in client.payloads[0]['spots']] == [1]


def test_pipeline_no_slot_evidence_sends_no_heartbeat(tmp_path):
    import numpy as np
    from test_agent import write_clip
    from pipeline import parse_args,run_pipeline
    video = tmp_path/'unknown.mp4'
    write_clip(video,[np.full((480,640,3),90,np.uint8)])
    class Detector:
        kind = 'slot_fixture'
        names = {0:'empty',1:'occupied'}
        def __call__(self, frame):
            return []
    args = parse_args(['--source',str(video),'--roi',str(Path(__file__).resolve().parents[2]/'edge/python-agent/configs/local-video.json')])
    result = run_pipeline(args,detector=Detector())
    assert result['emitted_payloads'] == 0
    assert result['final_zone']['unknown'] == 2
