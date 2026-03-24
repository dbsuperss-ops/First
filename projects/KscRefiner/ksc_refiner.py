#!/usr/bin/env python3
"""
KSC Refiner v1.3 - Main Entry Point
"""
import sys
import os

# 모듈 경로 추가
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from core.engine import main

if __name__ == "__main__":
    main()
