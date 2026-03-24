"""
KSC Refiner v1.3 - Parser Factory
법인코드에 따라 적절한 파서 생성
"""
from typing import Dict
import logging

from parsers.base_parser import BaseParser
from parsers.config_driven_parser import ConfigDrivenParser
from parsers.kctr_specialized_parser import KctrSpecializedParser


class ParserFactory:
    """파서 팩토리"""

    # 특수 파서 매핑 (특별한 처리가 필요한 법인)
    SPECIALIZED_PARSERS = {
        "KCTR": KctrSpecializedParser,
    }

    @staticmethod
    def create_parser(filepath: str, company_code: str,
                      parser_config: Dict, rates: Dict) -> BaseParser:
        """
        법인코드에 따라 적절한 파서 생성

        Args:
            filepath: 엑셀 파일 경로
            company_code: 법인 코드
            parser_config: 파서 설정
            rates: 환율 정보

        Returns:
            BaseParser 인스턴스
        """
        logger = logging.getLogger("ParserFactory")

        # 특수 파서가 있으면 사용
        if company_code in ParserFactory.SPECIALIZED_PARSERS:
            parser_class = ParserFactory.SPECIALIZED_PARSERS[company_code]
            logger.info(f"{company_code}: 전용 파서 사용 ({parser_class.__name__})")
        else:
            # 기본 설정 기반 파서 사용
            parser_class = ConfigDrivenParser
            logger.info(f"{company_code}: 범용 파서 사용")

        return parser_class(
            filepath=filepath,
            company_code=company_code,
            parser_config=parser_config,
            rates=rates
        )
