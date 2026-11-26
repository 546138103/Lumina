from .task_common import evaluate_text

QUESTIONS = (
    ("你叫什么名字呀？", "回答自己的名字或昵称即可，包括仅说名字。"),
    ("你今年几岁啦？", "回答自己的年龄即可，包括仅说年龄数字。"),
    ("你喜欢玩什么呀？", "回答自己喜欢的具体游戏、玩具或活动即可，包括仅说活动名称。"),
)


def matches_question(index, answer, api_key):
    prompt, rule = QUESTIONS[index]
    rules = rule + "仅判断本轮提问。额外信息不影响判定。答非所问、拒绝或含糊均不通过。"
    return evaluate_text(answer, prompt, rules, api_key).result == "yes"
