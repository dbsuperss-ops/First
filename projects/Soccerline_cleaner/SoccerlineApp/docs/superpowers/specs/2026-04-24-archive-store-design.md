# SoccerlineApp 영구 저장소(아카이브) 기능 설계

**작성일:** 2026-04-24
**대상 버전:** SoccerlineApp v0.3 (.NET 10 WPF)

---

## 1. 배경 및 목적

현재 SoccerlineApp 은 크롤 결과를 메모리(`_posts`) 에만 유지하며, 앱 종료 시 사라진다. Excel 내보내기가 있지만 이는 일회성 산출물이지 재사용 가능한 저장소가 아니다. 사용자는 여러 날에 걸쳐 크롤한 결과를 누적 보관하고, 필요할 때 불러와 기존 필터/내보내기 기능으로 재활용하길 원한다.

본 기능은 **수동 저장/수동 로드** 방식의 영구 아카이브를 추가한다. 저장 형식은 SQLite, 중복(같은 PostId)은 스킵, 로드는 현재 목록을 교체한다.

## 2. 범위

**포함**
- SQLite 기반 로컬 DB 파일 1개에 Post/Comment 저장
- 툴바의 수동 [Save to Archive] / [Load Archive] 버튼
- 저장 시 중복 PostId 는 건너뜀(기존 레코드 보존)
- 로드 시 현재 `_posts` 를 교체 (미저장 변경이 있으면 확인 팝업)
- 상태바에 현재 아카이브 레코드 수 표시

**제외 (이번 범위 밖)**
- 전문 검색(full-text search)
- 기간/게시판별 조건부 로드
- 자동 저장
- 아카이브 내 레코드 편집/삭제 UI (필요 시 DB 뷰어 도구 사용)

## 3. 아키텍처

### 3.1 파일 변경 목록

| 파일 | 변경 종류 | 내용 |
|------|-----------|------|
| `ArchiveStore.cs` | **신규** | SQLite 래퍼 클래스 |
| `MainWindow.xaml` | 수정 | 툴바 버튼 2개, 상태바 아카이브 개수 추가 |
| `MainWindow.xaml.cs` | 수정 | 버튼 핸들러, `_hasUnsavedWork` 플래그 |
| `SoccerlineApp.csproj` | 수정 | `Microsoft.Data.Sqlite` 패키지 참조 추가 |

### 3.2 DB 파일 위치

```csharp
System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "soccerline_archive.db")
```

기존 `crawler_config.txt` 와 동일한 위치. 실행 파일을 다른 PC로 옮기면 DB 도 함께 이동 가능.

### 3.3 ArchiveStore 인터페이스 (개요)

```csharp
public class ArchiveStore
{
    public ArchiveStore(string dbPath);   // 없으면 생성 + 스키마 초기화
    public int Count();                   // 저장된 Post 수 (상태바 표시용)
    public SaveResult Save(IEnumerable<Post> posts);  // 단일 트랜잭션, 중복 스킵
    public List<Post> LoadAll();          // Posts + Comments 조인 로드
}

public record SaveResult(int Inserted, int SkippedDuplicate, int SkippedNoPostId);
```

- 모든 DB 접근은 UI 스레드에서 동기 수행 (수백~수천 건 규모 기준 충분히 빠름).
- 차후 체감 지연이 생기면 `Task.Run` 으로 감싸는 것으로 개선 가능.

## 4. DB 스키마

```sql
CREATE TABLE IF NOT EXISTS Posts (
  PostId    TEXT PRIMARY KEY,
  BoardName TEXT NOT NULL,
  CreatedAt TEXT,
  Title     TEXT,
  Author    TEXT,
  AuthorId  TEXT,
  AuthorIp  TEXT,
  Views     TEXT,
  Likes     TEXT,
  Dislikes  TEXT,
  Link      TEXT,
  Body      TEXT,
  SavedAt   TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS Comments (
  PostId   TEXT NOT NULL,
  OrderIdx INTEGER NOT NULL,
  Nickname TEXT,
  UserID   TEXT,
  AuthorIp TEXT,
  CreatedAt TEXT,
  Content  TEXT,
  PRIMARY KEY (PostId, OrderIdx),
  FOREIGN KEY (PostId) REFERENCES Posts(PostId) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_posts_board_created ON Posts(BoardName, CreatedAt);
```

설계 근거:
- `Post` 모델의 `Views/Likes/Dislikes` 가 현재 `string` 이므로 DB 도 TEXT 로 맞춰 변환 버그 차단.
- `PostId` 는 `Link` 에서 `/board/(\d+)` 로 추출(`MainWindow.ExtractPostId` 재사용). 추출 불가 시 해당 Post 는 저장 스킵.
- 중복 저장은 `INSERT OR IGNORE INTO Posts ...` 로 자동 스킵. Comments 는 해당 Post 가 스킵되면 함께 스킵.

## 5. UI 변경 상세

### 5.1 툴바 버튼

기존 툴바에 두 버튼을 `btnExport` 근처에 추가.

```
[ Save to Archive ]   [ Load Archive ]
```

### 5.2 상태바

기존 `txtGridStatus` 옆에 `txtArchiveCount` 추가. 표시 예: `archive: 1,234`.

갱신 시점:
- 앱 시작 시 `ArchiveStore.Count()` 1회
- Save/Load 완료 직후

### 5.3 버튼 동작

**Save to Archive**
1. 저장 대상 선정: `_posts.Where(p => p.DetailsFetched)`.
2. 대상 0건이면 정보 팝업 `"상세 수집된 항목이 없습니다."` 후 종료.
3. `ArchiveStore.Save()` 호출.
4. 로그에 `[ARCHIVE] 신규 N건 저장, M건 스킵(중복), K건 스킵(PostId 없음)` 출력.
5. `_hasUnsavedWork = false` 로 세팅.
6. 상태바 아카이브 개수 갱신.

**Load Archive**
1. `_hasUnsavedWork == true` 면 확인팝업:
   `"현재 목록에 저장되지 않은 변경이 있습니다. 아카이브로 교체하시겠습니까?"`
   취소 시 종료.
2. `_posts.Clear()` → `_columnFilters.Clear()`.
3. `ArchiveStore.LoadAll()` 결과를 `_posts` 에 추가.
4. 로드된 각 `Post` 의 `DetailsFetched = true`, `IsSelected = true` 로 설정.
5. 로그에 `[ARCHIVE] N건 로드됨` 출력.
6. `_hasUnsavedWork = false`.

### 5.4 `_hasUnsavedWork` 플래그 규칙

- `true` 로 되는 시점: Stage 2 (`btnFetchDetails_Click`) 가 정상 완료되었을 때.
- `false` 로 되는 시점: Save/Load 성공 시.
- 기본값: `false` (빈 목록 상태).

Stage 1(목록만)에서 `true` 를 세우지 않는 이유: 목록만 있는 Post 는 어차피 저장 대상이 아니므로 잃어도 무방하다는 판단(사용자는 크롤 파라미터를 알고 있으므로 재실행 가능).

## 6. 에러 / 엣지케이스

| 상황 | 처리 |
|------|------|
| DB 파일 없음 | 첫 구동 시 `CREATE TABLE IF NOT EXISTS` 로 자동 생성 |
| DB 락/손상 (`SqliteException`) | MessageBox + 로그 `[CRITICAL] {메시지}`, 앱은 계속 동작 |
| `PostId` 추출 실패 | 해당 Post 만 저장 스킵, `SaveResult.SkippedNoPostId` 에 집계, 로그에 개별 경고 |
| 저장 도중 예외 | 단일 트랜잭션으로 감싸 rollback → 부분 저장 방지 |
| 로드 중 Comment 의 Post 가 Posts 에 없음 | 데이터 무결성 위반. FK 로 방지되지만 혹시 발생 시 해당 Comment 는 무시 + 로그 |
| 빈 아카이브 로드 | 일반 로드 흐름과 동일(미저장 변경 있으면 팝업은 그대로 뜸). 결과적으로 `_posts.Clear()` 만 수행되고 `[ARCHIVE] 0건 로드됨` 로그 |

## 7. 의존성 변경

`SoccerlineApp.csproj` 에 다음 추가:

```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="9.0.0" />
```

`Microsoft.Data.Sqlite` 9.x 는 .NET 8/9/10 호환. 구현 시 `dotnet add package` 로 최신 9.0.x 패치를 가져옴.

## 8. 테스트 관점

단위 테스트 프로젝트가 현재 없으므로, 수동 시나리오 테스트로 검증:

1. **첫 실행**: 앱 최초 기동 → `soccerline_archive.db` 자동 생성, 상태바 `archive: 0` 표시.
2. **저장 기본**: Stage 1 + Stage 2 수행 후 Save → DB 파일에 레코드 기록, 상태바 개수 증가, 로그 확인.
3. **중복 스킵**: 같은 게시판 같은 페이지 재크롤 후 Save → `스킵(중복)` 카운트 증가, 기존 레코드 변경 없음(DB 뷰어로 확인).
4. **로드 교체**: `_posts` 에 다른 내용 있는 상태에서 Load → 확인팝업 뜨고, OK 시 아카이브 내용으로 교체.
5. **로드 후 내보내기**: 로드된 Post 들을 Excel 내보내기 → 기존 내보내기 동작 동일.
6. **PostId 없는 Post**: Link 가 비정상인 가짜 Post 하나를 포함시켜 저장 → 해당 건만 스킵, 나머지는 정상 저장.
7. **댓글 보존**: 댓글이 있는 Post 저장 후 Load → Comment 순서/내용 동일.

## 9. 마이그레이션 / 호환성

- 기존 기능(Stage 1, Stage 2, Excel 내보내기, 컬럼 필터)은 변경 없이 유지.
- 기존 `crawler_config.txt` 저장 방식도 그대로.
- DB 스키마 v1 고정. 차후 컬럼 추가 시 `PRAGMA user_version` 으로 마이그레이션 처리 예정(이번 범위 밖).

## 10. 향후 확장 여지 (범위 밖)

- 기간/게시판 조건부 로드 → `LoadAll()` 을 `Load(DateTime from, DateTime to, string? board)` 로 확장.
- 본문/댓글 키워드 검색 → SQLite FTS5 가상 테이블 추가.
- 자동 저장 옵션 → Stage 2 완료 시 자동 호출.
- DB 경로 사용자 지정 → 설정 파일에 경로 추가.
