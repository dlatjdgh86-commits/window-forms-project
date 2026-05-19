"""
광운대 학사 TODO 자동 생성 시스템 - FastAPI 로컬 서버
실행: uvicorn main:app --reload --port 8000
"""

from fastapi import FastAPI, HTTPException, status
from fastapi.middleware.cors import CORSMiddleware
from contextlib import asynccontextmanager
import uvicorn
import uuid
from datetime import datetime, date

from schemas import (
    TodoCreate,
    TodoUpdate,
    TodoResponse,
    ScheduleResponse,
    GenerateTodoResponse,
    MessageResponse,
)


# ────────────────────────────────────────────────
# 임시 인메모리 DB (SQLite 연동 전까지 사용)
# ────────────────────────────────────────────────

fake_todos: dict[str, dict] = {}
fake_schedules: list[dict] = [
    {
        "id": "sched-001",
        "title": "2025학년도 1학기 중간고사",
        "start_date": date(2025, 4, 14),
        "end_date": date(2025, 4, 18),
        "category": "시험",
        "source": "https://www.kw.ac.kr",
        "raw_text": "2025학년도 1학기 중간고사 기간",
    },
    {
        "id": "sched-002",
        "title": "수강신청 (재수강/추가신청)",
        "start_date": date(2025, 3, 10),
        "end_date": date(2025, 3, 12),
        "category": "수강신청",
        "source": "https://www.kw.ac.kr",
        "raw_text": "2025-1 수강신청 재수강 및 추가신청",
    },
]


# ────────────────────────────────────────────────
# 앱 생명주기 (서버 시작 / 종료 시 실행)
# ────────────────────────────────────────────────

@asynccontextmanager
async def lifespan(app: FastAPI):
    # 서버 시작 시: 크롤러 초기 실행, DB 연결 등 초기화 작업
    print("🚀 서버 시작 - 학사 데이터 초기 로드 중...")
    # TODO: await crawler.run_initial_crawl()
    # TODO: db.init()
    yield
    # 서버 종료 시: 리소스 정리
    print("🛑 서버 종료")


# ────────────────────────────────────────────────
# FastAPI 앱 초기화
# ────────────────────────────────────────────────

app = FastAPI(
    title="광운대 학사 TODO API",
    description="학사 일정 크롤링 + LLM 기반 TODO 자동 생성 로컬 서버",
    version="0.1.0",
    lifespan=lifespan,
)

# C# WinForms 앱에서 localhost로 호출하므로 CORS 허용
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost", "http://127.0.0.1"],
    allow_methods=["*"],
    allow_headers=["*"],
)


# ────────────────────────────────────────────────
# 헬스체크
# ────────────────────────────────────────────────

@app.get("/", tags=["Health"])
def health_check():
    return {"status": "ok", "timestamp": datetime.now().isoformat()}


# ────────────────────────────────────────────────
# TODO 라우터 (routes/todos.py 로 분리 예정)
# ────────────────────────────────────────────────

@app.get(
    "/todos/generate",
    response_model=GenerateTodoResponse,
    tags=["TODO"],
    summary="AI TODO 자동 생성",
    description="저장된 학사일정을 기반으로 LLM이 TODO를 생성하여 반환합니다.",
)
def generate_todos(period_days: int = 30):
    """
    학사일정 → LLM API 호출 → TODO 리스트 반환
    period_days: 오늘부터 며칠치 일정을 참고할지 (기본 30일)
    """
    # TODO: LLM API 연동 후 실제 생성 로직으로 교체
    sample_todos = [
        TodoResponse(
            id=str(uuid.uuid4()),
            title="중간고사 준비 계획 세우기",
            due_date=date(2025, 4, 10),
            priority="high",
            category="학업",
            source_event="2025학년도 1학기 중간고사",
            is_done=False,
            created_at=datetime.now(),
        )
    ]
    return GenerateTodoResponse(
        todos=sample_todos,
        generated_count=len(sample_todos),
        based_on_schedules=["2025학년도 1학기 중간고사"],
    )


@app.get(
    "/todos",
    response_model=list[TodoResponse],
    tags=["TODO"],
    summary="저장된 TODO 전체 조회",
)
def get_todos(is_done: bool | None = None, priority: str | None = None):
    """
    저장된 TODO 목록 반환
    - is_done: True/False 필터 (없으면 전체)
    - priority: high/medium/low 필터 (없으면 전체)
    """
    todos = list(fake_todos.values())

    if is_done is not None:
        todos = [t for t in todos if t["is_done"] == is_done]
    if priority:
        todos = [t for t in todos if t["priority"] == priority]

    return todos


@app.post(
    "/todos",
    response_model=TodoResponse,
    status_code=status.HTTP_201_CREATED,
    tags=["TODO"],
    summary="TODO 수동 추가",
)
def create_todo(todo: TodoCreate):
    """사용자가 직접 TODO를 추가합니다."""
    new_todo = TodoResponse(
        id=str(uuid.uuid4()),
        **todo.model_dump(),
        is_done=False,
        created_at=datetime.now(),
    )
    fake_todos[new_todo.id] = new_todo.model_dump()
    return new_todo


@app.put(
    "/todos/{todo_id}",
    response_model=TodoResponse,
    tags=["TODO"],
    summary="TODO 수정 (완료 체크 포함)",
)
def update_todo(todo_id: str, update: TodoUpdate):
    """
    TODO 내용 수정 또는 완료(is_done) 토글
    변경하고 싶은 필드만 보내면 됩니다.
    """
    if todo_id not in fake_todos:
        raise HTTPException(status_code=404, detail="해당 TODO를 찾을 수 없습니다.")

    stored = fake_todos[todo_id]
    update_data = update.model_dump(exclude_unset=True)  # 보낸 필드만 업데이트
    stored.update(update_data)
    fake_todos[todo_id] = stored
    return stored


@app.delete(
    "/todos/{todo_id}",
    response_model=MessageResponse,
    tags=["TODO"],
    summary="TODO 삭제",
)
def delete_todo(todo_id: str):
    if todo_id not in fake_todos:
        raise HTTPException(status_code=404, detail="해당 TODO를 찾을 수 없습니다.")

    del fake_todos[todo_id]
    return {"message": f"TODO({todo_id}) 삭제 완료"}


# ────────────────────────────────────────────────
# 학사일정 라우터 (routes/schedules.py 로 분리 예정)
# ────────────────────────────────────────────────

@app.get(
    "/schedules",
    response_model=list[ScheduleResponse],
    tags=["학사일정"],
    summary="학사일정 조회",
    description="크롤링된 학사일정 목록을 반환합니다. 캐시 데이터를 우선 반환하며 refresh=true 시 재크롤링합니다.",
)
def get_schedules(refresh: bool = False):
    """
    refresh=True: 광운대 사이트 재크롤링 후 반환
    refresh=False (기본): 캐시된 데이터 반환
    """
    if refresh:
        # TODO: await crawler.crawl_schedules() 호출로 교체
        print("🔄 학사일정 재크롤링 요청됨 (미구현)")

    return fake_schedules


# ────────────────────────────────────────────────
# 직접 실행 시 (python main.py)
# WinForms 앱이 subprocess로 이 파일을 실행
# ────────────────────────────────────────────────

if __name__ == "__main__":
    uvicorn.run(
        "main:app",
        host="127.0.0.1",
        port=8000,
        reload=False,   # 배포/자동실행 시 False
        log_level="info",
    )
