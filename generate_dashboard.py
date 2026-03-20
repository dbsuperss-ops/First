#!/usr/bin/env python3
"""
재무 대시보드 자동 생성 스크립트

사용법:
    python3 generate_dashboard.py <csv파일경로> [테마색상]

예시:
    python3 generate_dashboard.py test.csv blue
    python3 generate_dashboard.py test.csv orange
    python3 generate_dashboard.py test.csv green
"""

import csv
import sys
import os
from collections import defaultdict
from datetime import datetime

# 색상 테마 정의
THEMES = {
    'blue': {
        'primary': '#0066ff',
        'secondary': '#00ffff',
        'tertiary': '#4169e1',
        'dark': '#0a1929',
        'dark2': '#132f4c',
        'light': '#b3d9ff',
        'name': 'Electric Blue'
    },
    'orange': {
        'primary': '#e76f51',
        'secondary': '#f4a261',
        'tertiary': '#e9c46a',
        'dark': '#264653',
        'dark2': '#1a1a2e',
        'light': '#e9c46a',
        'name': 'Sunset Boulevard'
    },
    'green': {
        'primary': '#10b981',
        'secondary': '#34d399',
        'tertiary': '#6ee7b7',
        'dark': '#064e3b',
        'dark2': '#065f46',
        'light': '#d1fae5',
        'name': 'Emerald Forest'
    },
    'purple': {
        'primary': '#8b5cf6',
        'secondary': '#a78bfa',
        'tertiary': '#c4b5fd',
        'dark': '#2e1065',
        'dark2': '#4c1d95',
        'light': '#e9d5ff',
        'name': 'Royal Purple'
    }
}

def read_csv_data(csv_path):
    """CSV 파일에서 데이터 읽기"""
    with open(csv_path, 'r', encoding='utf-8-sig') as f:
        reader = csv.DictReader(f)
        data = list(reader)
    return data

def aggregate_data(data):
    """데이터 집계"""
    corp_data = defaultdict(lambda: {'매출': 0, '매출원가': 0, '판매비': 0, '관리비': 0, '영업이익': 0})
    monthly_data = defaultdict(lambda: defaultdict(lambda: {'매출': 0, '영업이익': 0}))

    for row in data:
        corp = row['법인코드']
        month = row['귀속연월']
        account = row['계정과목']
        amount = int(row['KRW금액'].replace(',', '')) if row['KRW금액'] else 0

        if account == '매출액':
            corp_data[corp]['매출'] += amount
            monthly_data[month][corp]['매출'] += amount
        elif account == '매출원가':
            corp_data[corp]['매출원가'] += amount
        elif account == '판매비':
            corp_data[corp]['판매비'] += amount
        elif account == '관리비':
            corp_data[corp]['관리비'] += amount
        elif account == '영업이익':
            corp_data[corp]['영업이익'] += amount
            monthly_data[month][corp]['영업이익'] += amount

    return corp_data, monthly_data

def generate_html(corp_data, monthly_data, theme='blue', output_path='dashboard.html'):
    """HTML 대시보드 생성"""

    theme_colors = THEMES.get(theme, THEMES['blue'])

    # 총계 계산
    total_revenue = sum(d['매출'] for d in corp_data.values())
    total_profit = sum(d['영업이익'] for d in corp_data.values())
    avg_margin = (total_profit / total_revenue * 100) if total_revenue > 0 else 0

    # 법인별 이익률 계산 및 정렬
    corps_with_rate = []
    for corp, data in corp_data.items():
        rate = (data['영업이익'] / data['매출'] * 100) if data['매출'] > 0 else 0
        corps_with_rate.append({
            'name': corp,
            'revenue': data['매출'],
            'profit': data['영업이익'],
            'rate': rate
        })

    corps_with_rate.sort(key=lambda x: x['rate'], reverse=True)

    # 월별 데이터 추출
    months = sorted(monthly_data.keys())
    corps_list = sorted(corp_data.keys())

    monthly_sales = {}
    for corp in corps_list:
        monthly_sales[corp] = [monthly_data[m][corp]['매출'] for m in months]

    # HTML 템플릿
    html_template = f"""<!DOCTYPE html>
<html lang="ko">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>재무 대시보드 {datetime.now().year}</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Crimson+Pro:wght@200;400;700;900&family=DM+Mono:wght@300;400;500&display=swap" rel="stylesheet">
    <style>
        :root {{
            --primary: {theme_colors['primary']};
            --secondary: {theme_colors['secondary']};
            --tertiary: {theme_colors['tertiary']};
            --dark: {theme_colors['dark']};
            --dark2: {theme_colors['dark2']};
            --light: {theme_colors['light']};
        }}

        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            font-family: 'DM Mono', monospace;
            background: linear-gradient(135deg, var(--dark) 0%, var(--dark2) 50%, var(--dark) 100%);
            color: #fff;
            overflow-x: hidden;
            line-height: 1.6;
        }}

        @keyframes gradientShift {{
            0%, 100% {{ background-position: 0% 50%; }}
            50% {{ background-position: 100% 50%; }}
        }}

        body::before {{
            content: '';
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: linear-gradient(
                45deg,
                rgba(from var(--primary) r g b / 0.1) 0%,
                rgba(from var(--secondary) r g b / 0.1) 50%,
                rgba(from var(--tertiary) r g b / 0.1) 100%
            );
            background-size: 400% 400%;
            animation: gradientShift 15s ease infinite;
            pointer-events: none;
            z-index: 0;
        }}

        .container {{
            position: relative;
            z-index: 2;
            max-width: 1800px;
            margin: 0 auto;
            padding: 60px 40px;
        }}

        .hero {{
            margin-bottom: 120px;
        }}

        .hero h1 {{
            font-family: 'Crimson Pro', serif;
            font-size: clamp(60px, 10vw, 140px);
            font-weight: 900;
            line-height: 0.9;
            margin-bottom: 20px;
            background: linear-gradient(135deg, var(--primary), var(--secondary), var(--tertiary));
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            text-transform: uppercase;
            letter-spacing: -0.04em;
            animation: fadeInUp 1s ease-out;
        }}

        .hero .subtitle {{
            font-size: clamp(18px, 2vw, 28px);
            color: var(--light);
            font-weight: 300;
            margin-bottom: 40px;
            opacity: 0;
            animation: fadeInUp 1s ease-out 0.3s forwards;
        }}

        .hero .meta {{
            display: flex;
            gap: 60px;
            flex-wrap: wrap;
            opacity: 0;
            animation: fadeInUp 1s ease-out 0.6s forwards;
        }}

        .hero .meta-item {{
            font-size: 14px;
            text-transform: uppercase;
            letter-spacing: 0.1em;
            color: rgba(255, 255, 255, 0.5);
        }}

        .hero .meta-item span {{
            display: block;
            font-size: 24px;
            color: var(--secondary);
            font-weight: 500;
            margin-top: 8px;
        }}

        .stats-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
            gap: 30px;
            margin-bottom: 100px;
        }}

        .stat-card {{
            background: rgba(255, 255, 255, 0.03);
            border: 2px solid var(--primary);
            padding: 40px;
            position: relative;
            overflow: hidden;
            transition: all 0.4s;
            cursor: pointer;
        }}

        .stat-card:hover {{
            transform: translateY(-8px);
            border-color: var(--secondary);
            box-shadow: 0 20px 60px rgba(from var(--secondary) r g b / 0.3);
        }}

        .stat-card .label {{
            font-size: 12px;
            text-transform: uppercase;
            letter-spacing: 0.15em;
            color: var(--light);
            margin-bottom: 16px;
        }}

        .stat-card .value {{
            font-family: 'Crimson Pro', serif;
            font-size: clamp(42px, 5vw, 72px);
            font-weight: 900;
            line-height: 1;
            color: #fff;
            margin-bottom: 12px;
            transition: color 0.3s;
        }}

        .stat-card:hover .value {{
            color: var(--secondary);
        }}

        .stat-card .detail {{
            font-size: 14px;
            color: rgba(255, 255, 255, 0.6);
        }}

        .section-title {{
            font-family: 'Crimson Pro', serif;
            font-size: clamp(36px, 5vw, 64px);
            font-weight: 700;
            margin-bottom: 60px;
            color: var(--secondary);
            text-transform: uppercase;
        }}

        .revenue-section {{
            margin-bottom: 120px;
        }}

        .revenue-bars {{
            display: flex;
            flex-direction: column;
            gap: 40px;
        }}

        .revenue-bar {{
            opacity: 0;
            animation: slideInLeft 0.8s ease-out forwards;
        }}

        .revenue-bar:nth-child(1) {{ animation-delay: 0.1s; }}
        .revenue-bar:nth-child(2) {{ animation-delay: 0.2s; }}
        .revenue-bar:nth-child(3) {{ animation-delay: 0.3s; }}

        .revenue-bar-inner {{
            display: flex;
            align-items: center;
            gap: 30px;
            padding: 30px;
            background: rgba(255, 255, 255, 0.02);
            border-left: 6px solid var(--primary);
            transition: all 0.4s;
        }}

        .revenue-bar:hover .revenue-bar-inner {{
            background: rgba(255, 255, 255, 0.05);
            transform: translateX(20px);
        }}

        .revenue-name {{
            font-family: 'Crimson Pro', serif;
            font-size: 48px;
            font-weight: 700;
            min-width: 200px;
        }}

        .revenue-visual {{
            flex: 1;
            height: 80px;
            background: linear-gradient(90deg, var(--primary), transparent);
            position: relative;
        }}

        .revenue-amount {{
            position: absolute;
            right: 20px;
            top: 50%;
            transform: translateY(-50%);
            font-family: 'Crimson Pro', serif;
            font-size: 36px;
            font-weight: 900;
            color: #fff;
            text-shadow: 0 2px 10px rgba(0, 0, 0, 0.5);
        }}

        .profit-section {{
            margin-bottom: 120px;
        }}

        .profit-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
            gap: 40px;
        }}

        .profit-card {{
            background: linear-gradient(135deg, rgba(from var(--primary) r g b / 0.1), rgba(from var(--dark) r g b / 0.1));
            padding: 50px;
            position: relative;
            border: 1px solid rgba(255, 255, 255, 0.1);
            transition: all 0.5s;
            overflow: hidden;
        }}

        .profit-card:hover {{
            transform: scale(1.05);
            border-color: var(--secondary);
        }}

        .profit-rank {{
            position: absolute;
            top: 20px;
            right: 20px;
            width: 80px;
            height: 80px;
            border-radius: 50%;
            background: var(--primary);
            display: flex;
            align-items: center;
            justify-content: center;
            font-family: 'Crimson Pro', serif;
            font-size: 42px;
            font-weight: 900;
            color: #fff;
        }}

        .profit-company {{
            font-family: 'Crimson Pro', serif;
            font-size: 56px;
            font-weight: 900;
            margin-bottom: 20px;
        }}

        .profit-rate {{
            font-size: 72px;
            font-weight: 900;
            font-family: 'Crimson Pro', serif;
            background: linear-gradient(135deg, var(--primary), var(--secondary));
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            margin-bottom: 20px;
        }}

        .profit-details {{
            display: flex;
            flex-direction: column;
            gap: 12px;
            font-size: 16px;
            color: rgba(255, 255, 255, 0.7);
        }}

        .profit-details div {{
            display: flex;
            justify-content: space-between;
        }}

        .trend-section {{
            margin-bottom: 80px;
        }}

        .trend-canvas-wrapper {{
            background: rgba(255, 255, 255, 0.02);
            padding: 60px;
            border: 1px solid rgba(255, 255, 255, 0.1);
        }}

        canvas {{
            display: block;
            width: 100%;
            height: auto;
        }}

        footer {{
            text-align: center;
            padding: 60px 0;
            font-size: 14px;
            color: rgba(255, 255, 255, 0.4);
            border-top: 1px solid rgba(255, 255, 255, 0.1);
        }}

        @keyframes fadeInUp {{
            from {{
                opacity: 0;
                transform: translateY(30px);
            }}
            to {{
                opacity: 1;
                transform: translateY(0);
            }}
        }}

        @keyframes slideInLeft {{
            from {{
                opacity: 0;
                transform: translateX(-50px);
            }}
            to {{
                opacity: 1;
                transform: translateX(0);
            }}
        }}

        @media (max-width: 768px) {{
            .profit-grid {{
                grid-template-columns: 1fr;
            }}
            .revenue-bar-inner {{
                flex-direction: column;
                align-items: flex-start;
            }}
        }}
    </style>
</head>
<body>
    <div class="container">
        <header class="hero">
            <h1>Financial<br>Dashboard<br>{datetime.now().year}</h1>
            <div class="subtitle">글로벌 사업장 재무 성과 분석</div>
            <div class="meta">
                <div class="meta-item">
                    기간
                    <span>{min(months)} - {max(months)}</span>
                </div>
                <div class="meta-item">
                    사업장
                    <span>{len(corps_list)}개 법인</span>
                </div>
                <div class="meta-item">
                    총 매출
                    <span>{total_revenue // 100000000:,}억원</span>
                </div>
            </div>
        </header>

        <section class="stats-grid">
            <div class="stat-card">
                <div class="label">Total Revenue</div>
                <div class="value">{total_revenue / 1000000000:.1f}B</div>
                <div class="detail">연간 총 매출 (KRW)</div>
            </div>
            <div class="stat-card">
                <div class="label">Operating Profit</div>
                <div class="value">{total_profit / 1000000000:.1f}B</div>
                <div class="detail">연간 영업이익 (KRW)</div>
            </div>
            <div class="stat-card">
                <div class="label">Avg. Margin</div>
                <div class="value">{avg_margin:.2f}%</div>
                <div class="detail">평균 이익률</div>
            </div>
        </section>

        <section class="revenue-section">
            <h2 class="section-title">Revenue by Division</h2>
            <div class="revenue-bars">
"""

    # 매출 막대 추가
    max_revenue = max(d['매출'] for d in corp_data.values())
    for corp in sorted(corps_list, key=lambda c: corp_data[c]['매출'], reverse=True):
        revenue = corp_data[corp]['매출']
        width_pct = (revenue / max_revenue * 100)
        html_template += f"""                <div class="revenue-bar">
                    <div class="revenue-bar-inner">
                        <div class="revenue-name">{corp}</div>
                        <div class="revenue-visual" style="width: {width_pct}%;">
                            <div class="revenue-amount">{revenue // 100000000:,}억원</div>
                        </div>
                    </div>
                </div>
"""

    html_template += """            </div>
        </section>

        <section class="profit-section">
            <h2 class="section-title">Profitability Ranking</h2>
            <div class="profit-grid">
"""

    # 이익률 카드 추가
    for idx, corp_info in enumerate(corps_with_rate, 1):
        html_template += f"""                <div class="profit-card">
                    <div class="profit-rank">{idx}</div>
                    <div class="profit-company">{corp_info['name']}</div>
                    <div class="profit-rate">{corp_info['rate']:.2f}%</div>
                    <div class="profit-details">
                        <div>
                            <span>매출</span>
                            <span>{corp_info['revenue'] // 100000000:,}억원</span>
                        </div>
                        <div>
                            <span>영업이익</span>
                            <span>{corp_info['profit'] // 100000000:,}억원</span>
                        </div>
                    </div>
                </div>
"""

    html_template += """            </div>
        </section>

        <section class="trend-section">
            <h2 class="section-title">Monthly Sales Trend</h2>
            <div class="trend-canvas-wrapper">
                <canvas id="trendChart" width="1600" height="600"></canvas>
            </div>
        </section>

        <footer>
            <p>© """ + str(datetime.now().year) + """ Financial Dashboard · Auto-generated</p>
        </footer>
    </div>

    <script>
        const canvas = document.getElementById('trendChart');
        const ctx = canvas.getContext('2d');

        const salesData = """ + str(monthly_sales).replace("'", '"') + """;

        const months = """ + str([m[-2:] + '월' for m in months]).replace("'", '"') + """;
        const colors = {
"""

    # 색상 할당
    color_list = [theme_colors['primary'], theme_colors['tertiary'], theme_colors['secondary']]
    for idx, corp in enumerate(corps_list):
        html_template += f"            '{corp}': '{color_list[idx % len(color_list)]}',\n"

    html_template += """        };

        const padding = 80;
        const chartWidth = canvas.width - padding * 2;
        const chartHeight = canvas.height - padding * 2;

        const allValues = Object.values(salesData).flat();
        const maxValue = Math.max(...allValues);
        const minValue = 0;

        // Draw grid
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
        ctx.lineWidth = 1;
        for (let i = 0; i <= 5; i++) {
            const y = padding + (chartHeight / 5) * i;
            ctx.beginPath();
            ctx.moveTo(padding, y);
            ctx.lineTo(canvas.width - padding, y);
            ctx.stroke();

            const value = maxValue - (maxValue / 5) * i;
            ctx.fillStyle = 'rgba(255, 255, 255, 0.5)';
            ctx.font = '16px "DM Mono"';
            ctx.textAlign = 'right';
            ctx.fillText((value / 100000000).toFixed(0) + '억', padding - 15, y + 5);
        }

        // X-axis labels
        months.forEach((month, i) => {
            const x = padding + (chartWidth / (months.length - 1)) * i;
            ctx.fillStyle = 'rgba(255, 255, 255, 0.5)';
            ctx.font = '14px "DM Mono"';
            ctx.textAlign = 'center';
            ctx.fillText(month, x, canvas.height - padding + 30);
        });

        // Draw lines
        Object.keys(salesData).forEach(corp => {
            const data = salesData[corp];

            ctx.strokeStyle = colors[corp];
            ctx.lineWidth = 4;
            ctx.lineCap = 'round';
            ctx.shadowColor = colors[corp];
            ctx.shadowBlur = 20;
            ctx.beginPath();

            data.forEach((value, i) => {
                const x = padding + (chartWidth / (data.length - 1)) * i;
                const y = padding + chartHeight - ((value - minValue) / (maxValue - minValue)) * chartHeight;

                if (i === 0) {
                    ctx.moveTo(x, y);
                } else {
                    ctx.lineTo(x, y);
                }
            });

            ctx.stroke();
            ctx.shadowBlur = 0;

            // Points
            data.forEach((value, i) => {
                const x = padding + (chartWidth / (data.length - 1)) * i;
                const y = padding + chartHeight - ((value - minValue) / (maxValue - minValue)) * chartHeight;

                ctx.fillStyle = colors[corp];
                ctx.beginPath();
                ctx.arc(x, y, 6, 0, 2 * Math.PI);
                ctx.fill();

                ctx.strokeStyle = '""" + theme_colors['dark'] + """';
                ctx.lineWidth = 3;
                ctx.stroke();
            });
        });

        // Legend
        const legendY = 40;
        Object.keys(colors).forEach((corp, i) => {
            const x = padding + i * 150;

            ctx.strokeStyle = colors[corp];
            ctx.lineWidth = 4;
            ctx.beginPath();
            ctx.moveTo(x, legendY);
            ctx.lineTo(x + 40, legendY);
            ctx.stroke();

            ctx.fillStyle = '#fff';
            ctx.font = 'bold 18px "DM Mono"';
            ctx.textAlign = 'left';
            ctx.fillText(corp, x + 50, legendY + 6);
        });
    </script>
</body>
</html>"""

    # 파일 저장
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write(html_template)

    print(f"✅ 대시보드가 생성되었습니다: {output_path}")
    print(f"🎨 테마: {theme_colors['name']}")
    print(f"📊 법인 수: {len(corps_list)}")
    print(f"💰 총 매출: {total_revenue // 100000000:,}억원")

def main():
    if len(sys.argv) < 2:
        print("사용법: python3 generate_dashboard.py <csv파일경로> [테마색상]")
        print("\n사용 가능한 테마:")
        for theme_name, theme_data in THEMES.items():
            print(f"  - {theme_name}: {theme_data['name']}")
        sys.exit(1)

    csv_path = sys.argv[1]
    theme = sys.argv[2] if len(sys.argv) > 2 else 'blue'

    if not os.path.exists(csv_path):
        print(f"❌ 파일을 찾을 수 없습니다: {csv_path}")
        sys.exit(1)

    if theme not in THEMES:
        print(f"❌ 알 수 없는 테마: {theme}")
        print("사용 가능한 테마:", ", ".join(THEMES.keys()))
        sys.exit(1)

    print(f"📂 CSV 파일 읽는 중: {csv_path}")
    data = read_csv_data(csv_path)

    print(f"📊 데이터 집계 중...")
    corp_data, monthly_data = aggregate_data(data)

    output_path = f"dashboard_{theme}_{datetime.now().strftime('%Y%m%d_%H%M%S')}.html"

    print(f"🎨 대시보드 생성 중...")
    generate_html(corp_data, monthly_data, theme, output_path)

if __name__ == '__main__':
    main()
