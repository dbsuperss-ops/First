"""
KSC Refiner v1.3 - Configuration Loader
설정 파일 로딩 및 관리
"""
import os
import sys
import json
import yaml
from typing import Dict, Any


class ConfigLoader:
    """설정 파일 로더"""

    def __init__(self, config_dir: str = None):
        """
        Args:
            config_dir: 설정 디렉토리 경로 (None이면 자동 감지)
        """
        if config_dir is None:
            config_dir = self._get_config_dir()
        self.config_dir = config_dir
        self._parser_config = None
        self._rates = None

    def _get_config_dir(self) -> str:
        """설정 디렉토리 자동 감지"""
        if getattr(sys, 'frozen', False):
            # PyInstaller로 패키징된 경우
            base_path = sys._MEIPASS
        else:
            # 일반 Python 실행 시
            base_path = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        return os.path.join(base_path, "config")

    def load_parser_config(self) -> Dict[str, Any]:
        """파서 설정 로드 (캐싱)"""
        if self._parser_config is not None:
            return self._parser_config

        config_path = os.path.join(self.config_dir, "parser_config.yaml")
        if not os.path.exists(config_path):
            raise FileNotFoundError(f"Parser config not found: {config_path}")

        with open(config_path, "r", encoding="utf-8") as f:
            self._parser_config = yaml.safe_load(f)

        return self._parser_config

    def load_rates(self, year: str = "2026") -> Dict[str, float]:
        """환율 정보 로드"""
        if self._rates is None:
            rates_path = os.path.join(self.config_dir, "rates.json")
            if not os.path.exists(rates_path):
                raise FileNotFoundError(f"Rates config not found: {rates_path}")

            with open(rates_path, "r", encoding="utf-8") as f:
                self._rates = json.load(f)

        # 요청된 연도의 환율 반환, 없으면 2026년 기본값
        return self._rates.get(str(year), self._rates.get("2026"))

    def get_parser_config(self, company_code: str) -> Dict[str, Any]:
        """특정 법인의 파서 설정 반환"""
        config = self.load_parser_config()
        parsers = config.get("parsers", {})
        if company_code not in parsers:
            raise ValueError(f"No parser configuration for company: {company_code}")
        return parsers[company_code]

    def get_all_company_codes(self) -> list:
        """설정된 모든 법인코드 목록"""
        config = self.load_parser_config()
        return list(config.get("parsers", {}).keys())
