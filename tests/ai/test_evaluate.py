import pytest
from evaluate import evaluate


def label(name, start, end, status):
    return dict(spot_number=name, start_seconds=start, end_seconds=end, status=status)


def test_metrics_missing_frames_and_half_open_boundaries():
    labels = [label('A', 0, 1, 'occupied'), label('A', 1, 3, 'available'),
              label('B', 0, 3, 'available')]
    records = [dict(kind='observation', media_seconds=0, states={'A': 'available', 'B': 'occupied'}),
               dict(kind='observation', media_seconds=1, states={'A': 'available'}),
               dict(kind='frame_rejected', media_seconds=2)]
    result = evaluate(records, labels)
    assert result['confusion'] == dict(tp=0, tn=1, fp=1, fn=1)
    assert result['coverage'] == .5
    assert result['count_complete_frames'] == 1
    assert result['occupancy_count_mae'] == 0  # Counts can agree while slot states are wrong.


def test_unknown_and_empty_evidence_are_not_success():
    result = evaluate([dict(kind='observation', media_seconds=0, states={'A': 'occupied'})],
                      [label('A', 0, 1, 'unknown')])
    assert result['accuracy'] is None
    assert result['coverage'] is None
    assert result['occupancy_count_mae'] is None


def test_reject_ambiguous_ground_truth():
    with pytest.raises(ValueError, match='Overlapping'):
        evaluate([], [label('A', 0, 2, 'occupied'), label('A', 1, 3, 'available')])
    with pytest.raises(ValueError):
        evaluate([], [label('A', 0, float('nan'), 'occupied')])
