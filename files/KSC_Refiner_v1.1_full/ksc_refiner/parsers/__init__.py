from abc import ABC, abstractmethod
from typing import List
from schema import AccountRow

class BaseParser(ABC):
    def __init__(self, filepath: str, rates: dict):
        self.filepath = filepath
        self.rates = rates

    @abstractmethod
    def extract(self) -> List[AccountRow]:
        pass

    def convert_krw(self, amount: float, currency: str) -> float:
        rate = self.rates.get(currency, 1)
        return amount * rate

    def make_row(self, ym, corp, cat, sub, account, currency, local_amount, data_type="실적"):
        rate = self.rates.get(currency, 1)
        return AccountRow(
            귀속연월=ym,
            법인코드=corp,
            데이터타입=data_type,
            대분류=cat,
            중분류=sub,
            계정과목=account,
            현지통화=currency,
            현지금액=round(local_amount, 2),
            적용환율=rate,
            KRW금액=round(local_amount * rate, 0)
        )
