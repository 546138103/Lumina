"""共用文本判定：API 仅返回 result，程序补充原文和本轮提示。"""
import json
import os
from typing import Literal

from openai import OpenAI
from pydantic import BaseModel, ConfigDict, Field


class Assessment(BaseModel):
    model_config = ConfigDict(extra='forbid')
    result: Literal['yes', 'no'] = Field(
        description='yes=满足当前语言目标；no=孩子明确拒绝，或者回答为空、无关、信息不足，未满足本轮问题的目标。')


class TaskResult(Assessment):
    evidence: str = Field(description='孩子的完整原始输入，由程序记录')
    manually_issue: str = Field(description='人工设置的问题，由程序记录')
    model: str = Field(description='本次请求配置的模型型号，由程序记录；空回答时不调用 API')


COMMON_RULES = '''
你是儿童社交互动游戏中的回答判定器。每轮会收到一个人工设置的问题和孩子的回答。

先判断问题在问什么，再根据孩子的回答填写一个字段。对于邀请孩子介绍姓名、年龄、喜好的综合自我介绍题，
表达其中任意一项具体信息即为 yes，不要求三项齐全。名字或昵称、年龄数字、具体玩具或活动都可以是简短回答。
只有问候、空泛回答或重复老师的问题为 no。

result
   - yes：孩子的回答满足本轮问题的目标。
   - no：孩子明确拒绝，或者回答为空、无关、信息不足，未满足本轮问题的目标。
'''


def evaluate_text(child_answer: str, manually_issue: str, api_key: str) -> TaskResult:
    if not isinstance(child_answer, str):
        raise TypeError('child_answer 必须是字符串')
    if not isinstance(manually_issue, str):
        raise TypeError('manually_issue 必须是字符串')
    if not manually_issue.strip():
        raise ValueError('manually_issue 不能为空，请传入本轮人工设置的提示')
    model = os.getenv('ARK_MODEL') or 'doubao-seed-2-1-lite-260915'
    if not child_answer.strip():
        return TaskResult(result='no',
                          evidence=child_answer, manually_issue=manually_issue, model=model)

    if not api_key:
        raise RuntimeError('请先设置环境变量 ARK_API_KEY')
    with OpenAI(base_url='https://ark.cn-beijing.volces.com/api/v3', api_key=api_key,
                timeout=30.0, max_retries=2) as client:
        response = client.responses.parse(
            model=model,
            input=[{'role': 'system', 'content': COMMON_RULES},
                   {'role': 'user', 'content': json.dumps(
                       {'manually_issue': manually_issue, 'child_answer': child_answer}, ensure_ascii=False)}],
            text_format=Assessment,
            extra_body={"thinking": {"type": "disabled"}},
        )
    assessment = response.output_parsed
    if assessment is None:
        raise RuntimeError('API 未返回可解析的结构化结果；不能据此判断孩子未完成')
    return TaskResult(**assessment.model_dump(), evidence=child_answer, manually_issue=manually_issue, model=model)
