from pathlib import Path
import sys

AGENT = Path(__file__).resolve().parents[2] / "edge" / "python-agent"
sys.path.insert(0, str(AGENT))
