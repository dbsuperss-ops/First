"""
KSC Refiner v1.3 - Base Parser
모든 파서의 기본 클래스
"""
from abc import ABC, abstractmethod
from typing import List, Dict
import logging

from core.schema import AccountRow


class BaseParser(ABC):
    """파서 기본 클래스"""

    def __init__(self, filepath: str, company_code: str,
                 parser_config: Dict, rates: Dict[str, float]):
        """
        Args:
            filepath: 엑셀 파일 경로
            company_code: 법인 코드 (KSCP, KCTR, ...)
            parser_config: 파서 설정 (YAML에서 로드)
            rates: 환율 정보
        """
        self.filepath = filepath
        self.company_code = company_code
        self.config = parser_config
        self.rates = rates
        self.logger = logging.getLogger(f"Parser.{company_code}")

    @abstractmethod
    def extract(self) -> List[AccountRow]:
        """
        데이터 추출 (서브클래스에서 구현)

        Returns:
            AccountRow 리스트
        """
        pass

    def convert_to_krw(self, amount: float, currency: str) -> float:
        """
        현지 통화를 KRW로 변환

        Args:
            amount: 현지 금액
            currency: 통화 코드

        Returns:
            KRW 금액
        """
        rate = self.rates.get(currency, 1.0)
        return amount * rate

    def make_row(self, year_month: str, category: str, subcategory: str,
                 account: str, local_amount: float,
                 data_type: str = "실적") -> AccountRow:
        """
        AccountRow 생성 헬퍼

        Args:
            year_month: 귀속연월 (YYYY-MM)
            category: 대분류 (PL, MC)
            subcategory: 중분류
            account: 계정과목
            local_amount: 현지 금액
            data_type: 데이터타입 ("실적" or "계획")

        Returns:
            AccountRow 객체
        """
        currency = self.config.get("currency", "KRW")
        rate = self.rates.get(currency, 1.0)
        krw_amount = local_amount * rate

        return AccountRow(
            귀속연월=year_month,
            법인코드=self.company_code,
            데이터타입=data_type,
            대분류=category,
            중분류=subcategory,
            계정과목=account,
            현지통화=currency,
            현지금액=round(local_amount, 2),
            적용환율=rate,
            KRW금액=round(krw_amount, 0)
        )

    def log_info(self, message: str):
        """정보 로그"""
        self.logger.info(message)

    def log_warning(self, message: str):
        """경고 로그"""
        self.logger.warning(message)

    def log_error(self, message: str):
        """에러 로그"""
        self.logger.error(message)
