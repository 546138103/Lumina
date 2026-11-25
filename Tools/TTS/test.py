from fastapi import FastAPI
import asyncio
import uvicorn

app = FastAPI()


@app.get("/fast")
async def fast():
    return {"message": "立即返回"}


@app.get("/slow")
async def slow():
    print("开始处理请求")

    await asyncio.sleep(5)

    print("请求处理完成")

    return {"message": "等待5秒后返回"}


@app.get("/health")
async def health():
    return {"status": "ok"}


if __name__ == "__main__":
    uvicorn.run(
        app,
        host="127.0.0.1",
        port=8000
    )