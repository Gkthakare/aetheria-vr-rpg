#!/usr/bin/env python3
"""
Aetheria VR - Gesture Data Collection (Outline)

Collect hand pose data for ML gesture recognition.
Run this while playing in VR; labels come from keyboard (1-6) or from Unity log.

Labels: 0=None, 1=Pinch, 2=Fist, 3=OpenPalm, 4=Point, 5=Thrust, 6=Shield

Data format: CSV with columns [label, lx, ly, lz, rx, ry, rz, ...] for 21 joints × 2 hands
"""

import argparse
import csv
from pathlib import Path
from datetime import datetime


LABELS = ["None", "Pinch", "Fist", "OpenPalm", "Point", "Thrust", "Shield"]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", default="./gesture_data.csv", help="Output CSV")
    parser.add_argument("--port", type=int, default=8765, help="Port for Unity to send poses")
    args = parser.parse_args()

    # TODO: Add HTTP server or socket to receive poses from Unity
    # Unity sends: {"label": 1, "left": [...], "right": [...]}
    # This script appends to CSV

    print("Gesture collection outline.")
    print("Labels: 0=None, 1=Pinch, 2=Fist, 3=OpenPalm, 4=Point, 5=Thrust, 6=Shield")
    print("Unity needs to send hand joint positions (from XR Hands) to this script.")
    print("See ML_AI_ROADMAP.md for full pipeline.")


if __name__ == "__main__":
    main()
