# Lumina｜本地 TTS 模型选型：安装接入与测试实施方案

版本 v1.0｜2026-10-13｜**执行计划，尚无模型实测结果**

## 1. 目的、场景与技术边界

目标：把 Lumina 游戏**预先写好的 NPC 台词**，合成为能区分角色、符合儿童训练情境、自然清晰且舒适的本地音频。**只在制作期**运行模型；游戏运行时 Unity 读取已审核 WAV/OGG 音频文件，按台词 ID 播放。模型不决定说什么、不理解儿童话、不实时生成分支；儿童 ASR 与语义 API 独立。

候选及比较重点：

| 候选 | 值得测的能力 | 成本/局限 | 建议地位 |
|---|---|---|---|
| Qwen3-TTS VoiceDesign | 用语言描述角色音色；不用先寻找参考录音，可尝试“温柔成熟教师”“活泼同伴” | 文字设计声音可能在不同句子间漂移；精确童声年龄不可承诺 | 重点候选A |
| CosyVoice3 | 通过**获得授权的参考音频**保持角色身份，可设置风格/情绪指令 | 找合适参考音、环境依赖、指令效果需试听 | 重点候选B |
| MeloTTS 中文 ONNX＋sherpa-onnx | 轻量、离线、Windows部署基线 | 中文示例音色少，角色可区分性不足 | 轻量对照候选 |

选择模型不等于确定商业使用权：逐项审核权重许可、代码许可、参考声授权、可合成声音使用条款。不能把“儿童音色”当作可精确设为 5 岁的数值能力。

## 2. Windows 安装前准备（先检查机器，再决定方案）

1. 记录 `Windows版本 / CPU / 内存 / GPU型号 / 显存 / NVIDIA驱动 / 可用磁盘 / Python版本`，命令可用 `python --version`、`nvidia-smi`（仅 NVIDIA）、任务管理器。
2. 模型制作期建议**独立 Python 环境**，别装进 Unity 工程的普通运行环境；不同仓库可能依赖冲突，按模型分别建 `.venv`/conda 环境。
3. 下载模型权重并校验目录文件完整性。首次安装需要网络；真正验证离线合成时必须在权重和依赖准备好之后断网重跑。
4. 保存以下环境证据：仓库 commit、权重版本/哈希、依赖锁定、GPU信息、生成脚本、声音配置以及许可确认。
5. 不要在儿童正式使用电脑上跑未优化的全套模型；本项目采用离线导出，因此模型推理的**首包时延**优先级低于质量、稳定和返工成本。

## 3. Qwen3-TTS VoiceDesign：接入步骤

源码主页（安装前核对当前 README/模型卡/具体入口）：https://github.com/QwenLM/Qwen3-TTS 。选择支持 VoiceDesign 的**准确权重版本**，不要把 Base/CustomVoice 与 VoiceDesign 混为同一个入口。

### 3.1 安装顺序
1. 创建独立虚拟环境，选择该仓库 README 推荐的 Python/torch/CUDA 版本（不要强行套用 CosyVoice 版本）。
2. 按 README 安装 Qwen3-TTS 包与匹配 GPU 的 PyTorch，取得 VoiceDesign 权重；验证依赖能导入并装载模型。
3. 单条冒烟：文本 `“你好呀！欢迎来到魔法幼儿园！”`，声音描述 `“温柔、清晰、自然、亲切的成年女教师音色；语速平缓；音量适中”`。
4. 将输出保存为 WAV，记录真实模型 ID、声音描述原文、生成次数、采样率、耗时。
5. 多句测试：同一个描述对 5 句不同台词分别生成3次，判断音色是否稳定；若不稳定，查看官方是否提供**固定/复用声音身份**的支持路径，不能凭固定随机种子就认定声音一致。

**调用模板（伪代码，需按固定仓库提交的真实 API 修改；不要直接当作现成脚本运行）**：

```python
model = load_qwen3_tts_voice_design(model_dir="models/VOICE_DESIGN_EXACT_ID")
for line in script_lines:
    wav = generate_with_voice_description(
        model=model,
        text=line["text"],
        language="Chinese",
        voice_description=voice_profiles[line["character"]],
    )
    save_wav(wav, f'output/{line["line_id"]}.wav')
```

**为什么这里不伪造实际函数名**：仓库安装、不同版本接口和权重名称可能变动。Codex 需读取锁定版本的官方 example，制作 `qwen_tts_adapter.py`，验收标准为脚本在你的本机真正生成、播放并导出音频，而不是复制一段看起来专业但无法运行的接口。

## 4. CosyVoice3：可对照官方入口的接入

官方仓库：https://github.com/QwenAudio/CosyVoice ，示例：https://github.com/QwenAudio/CosyVoice/blob/main/example.py ，推理入口：https://github.com/QwenAudio/CosyVoice/blob/main/cosyvoice/cli/cosyvoice.py 。

### 4.1 安装

```powershell
git clone https://github.com/QwenAudio/CosyVoice.git
cd CosyVoice
# 按仓库 README 创建其推荐的 Python 环境并安装 requirements
# 模型下载到 pretrained_models/Fun-CosyVoice3-0.5B 等与权重匹配的位置
# Windows 原生依赖遇到不兼容时评估 WSL2/Linux，不能承诺安装必然成功
```

### 4.2 生成 WAV 的示例（使用仓库现有 `AutoModel` 和 `inference_instruct2`）

以下片段放在仓库根目录执行，**前提是权重和授权参考音频已准备好、依赖安装成功**；模型目录以实际下载路径为准：

```python
import sys
sys.path.append('third_party/Matcha-TTS')
from cosyvoice.cli.cosyvoice import AutoModel
import torchaudio

model = AutoModel(model_dir='pretrained_models/Fun-CosyVoice3-0.5B')
reference = './voices/teacher_authorized.wav'
text = '没关系，我们再试一次，好不好？'
style = 'You are a helpful assistant. 请用温柔、鼓励的语气说这句话。<|endofprompt|>'
parts = []
for part in model.inference_instruct2(text, style, reference, stream=False):
    parts.append(part['tts_speech'].cpu())
if not parts:
    raise RuntimeError('没有生成音频')
import torch
wave = torch.cat(parts, dim=1)
torchaudio.save('teacher_test.wav', wave, model.sample_rate)
```

同一角色在测试中统一参考音；儿童同伴换成另一个**授权且匹配目标音色**的参考录音。`inference_zero_shot` 需要参考文本和参考音频配套；不要混淆 `inference_instruct2` 的参数。参考音色并不天然拥有他人的声音使用权。不同声音生成路径要**单独登记**，不把 300M-SFT 的预设音色能力直接说成 CV3 的独立性别开关。

## 5. MeloTTS + sherpa-onnx：轻量基线接入

官方 sherpa-onnx 中文 VITS/Melo 模型列表：https://k2-fsa.github.io/sherpa/onnx/tts/pretrained_models/vits.html ，Windows 安装：https://k2-fsa.github.io/sherpa/onnx/install/windows.html 。

安装方法：按官网 Windows 对应架构安装 sherpa-onnx Python 包/预编译程序，下载其说明中**完整的** `vits-melo-tts-zh_en` 模型、tokens、词典等文件，采用官方示例运行一条合成并保存 WAV。不要只下载 `.onnx` 就假设能运行；官方示例命令中的路径、说话人数量和采样率随具体包配置核对。原文档列示该中文模型只有 1 名说话人，因此重点测它能否达到最低的清晰度，而非拿它竞争多声线设计能力。

该候选定位为部署复杂度/CPU可行性的对照基线；如仅单一声音无法支撑教师及儿童同伴的区分，可在角色匹配硬门槛上淘汰。

## 6. 角色定义、声音描述和参数冻结

| 角色ID | 目标人设（主观试听目标） | 目标听感 | 不应发生 |
|---|---|---|---|
| TEACHER | 成年女教师，友好、可靠 | 自然、清晰、温和鼓励、适中语速 | 尖叫式鼓励、过快、机器人腔 |
| PEER_A | 活泼同伴，偏年轻音色 | 活泼但不刺耳，语音清楚 | 刺耳童声、过度夸张 |
| PEER_B | 安静同伴，偏柔和音色 | 与A可区分、平稳 | 与教师完全同声/忽高忽低 |

**公平性有两层**：对所有模型使用**同一台词、同一角色目标、同一输出格式、同一机器**；允许每个模型使用最适合它的原生音色控制方式（VoiceDesign描述 vs CosyVoice参考声），但各自需冻结并记录声音描述、参考音、随机种子（若可设）、语速、模型版本。否则比的是错误使用方法，而不是模型实际价值。

## 7. 具体测试台词集怎样安排（30 条主集）

完整台词可由下表作为数据构造配额，每个台词都有 line_id、speaker_id、情绪目标、长度档、包含的数字/姓名、声学风险标签。以下所列句子为**游戏脚本内测试样本或贴近脚本的可复现示例**；如最终游戏文案不同，应以冻结剧本替换，保留类型配额。

| 组别 | 数量 | 代表台词 | 测什么 |
|---|---:|---|---|
| 欢迎与问候 | 5 | “你好呀！欢迎来到魔法幼儿园！” | 自然度、亲和力 |
| 指令与引导 | 6 | “先走到讲台，再转过身来看看大家。” | 指令清晰度、停顿 |
| 温柔安抚 | 5 | “没关系，我们可以再试一次。” | 情绪舒适度、克制 |
| 鼓励成功 | 4 | “太棒啦！你成功找到新朋友啦！” | 兴奋而不尖锐 |
| 同伴请求与对话 | 5 | “我可以和你们一起搭积木吗？” | 角色匹配、问句语调 |
| 特殊读法 | 5 | “我叫小言，今年五岁，我喜欢搭积木。” | 姓名、数字、标点、长句 |
| **合计** | **30** | 教师15句、PEER_A 10句、PEER_B 5句 | 跨角色覆盖 |

### 7.1 可直接写入 `lines.csv` 的 30 句

```csv
line_id,role,category,text,emotion
T01,TEACHER,welcome,你好呀！欢迎来到魔法幼儿园！,warm
T02,TEACHER,welcome,今天是你来到这里的第一天。,warm
T03,TEACHER,welcome,你愿意走到讲台上介绍自己吗？,inviting
T04,TEACHER,welcome,大家都很想认识你呢！,happy
T05,TEACHER,welcome,准备好了吗？我们的魔法冒险开始啦！,happy
T06,TEACHER,instruction,可以向前走到老师旁边。,neutral
T07,TEACHER,instruction,再转过身来看看大家。,neutral
T08,TEACHER,instruction,你可以先说自己的名字。,neutral
T09,TEACHER,instruction,再告诉大家你今年几岁了。,neutral
T10,TEACHER,instruction,你还喜欢玩什么玩具呢？,inviting
T11,TEACHER,instruction,可以走过去和新朋友打个招呼哦。,neutral
T12,TEACHER,comfort,没关系，我们再试一次，好不好？,comfort
T13,TEACHER,comfort,你可以先看看大家在玩什么。,comfort
T14,TEACHER,comfort,如果现在不想一起玩，也没关系。,comfort
T15,TEACHER,comfort,老师会陪着你一起试一试。,comfort
P01,PEER_A,praise,太棒啦！你做到了！,happy
P02,PEER_A,praise,你成功找到新朋友啦！,happy
P03,PEER_A,praise,我们的城堡越来越漂亮啦！,happy
P04,PEER_A,praise,谢谢你帮我一起收拾！,happy
P05,PEER_A,greet,你好呀！很高兴认识你！,friendly
P06,PEER_A,request,我可以和你们一起搭积木吗？,curious
P07,PEER_A,request,你可以帮我把红色积木拿过来吗？,inviting
P08,PEER_A,request,我们一起玩小汽车吧！,happy
P09,PEER_A,request,哎呀！我的积木都掉到地上啦！,surprise
P10,PEER_A,special,我叫小言，今年五岁，我喜欢搭积木。,neutral
P11,PEER_B,dialogue,原来你也喜欢搭积木呀！,friendly
P12,PEER_B,dialogue,你想和我们一起玩吗？,inviting
P13,PEER_B,dialogue,我们正在玩过家家呢！,friendly
P14,PEER_B,dialogue,不客气，我们是好朋友呀！,warm
P15,PEER_B,special,先拿起一块积木，再把它放进盒子里。,neutral
```

以上 `csv` 中标号 P11–P15 对应 PEER_B，整体分配为教师15、A10、B5。**注意**：这 30 行是待批准的测试语料样本，含可根据项目脚本调整的示范性台词，不表示全部是游戏原文逐字复制。

### 7.2 实际运行批次

- 预筛：每模型 10 句（短/中/长及三角色、各类情绪）× 2 次，筛能否合成和主要缺陷。
- 正式：30 句 × 3 次 = **90 份候选音频/模型**；三模型共270份；保留全部原始结果及生成失败记录。模型可以额外制作“每句挑选最好的一版”的交付集，但要与“原始一次成功率”分别报告。
- 跨句稳定：同一角色连续听**5句不同句子**，比较声音是否像同一个人；同一台词重复3次看波动。
- 资源消耗：冷启动加载一次＋每句合成耗时；测每30句总耗时、p50/p95、GPU峰值、CPU/内存及崩溃；**不要把合成耗时混同为游戏播放延迟**。
- 离线性：断开网络后重新启动并合成至少3句；若失败，查明是否仍在下载权重、tokenizer或调用远程依赖。

## 8. 怎么评价：主观盲听＋客观审计＋工程指标

### 8.1 盲听安排

至少3位评价者，最好包括熟悉儿童教育/训练场景的人员。由管理员生成音频匿名 ID，随机化模型和播放顺序，同一输出响度标准化，但**保留原音量/削波记录**。使用相同播放器、耳机/音箱、音量。每个评分维度1–5分，记录具体缺陷；先独立评价、再讨论分歧。

| 指标 | 评分说明 | 负面例子 |
|---|---|---|
| 发音准确与可懂度 | 每个字词、数字、姓名是否听得清 | 漏字、重读、音节吞掉 |
| 自然度 | 像自然说话而非合成腔 | 断句诡异、节奏机械 |
| 角色匹配 | 教师/同伴的年龄感、性别感、性格是否符合设定 | 教师像儿童、两个同伴无法区分 |
| 音色稳定 | 相同角色跨句是否保持同一声音 | 每句话像不同人 |
| 情绪/语气 | 开心、安抚、惊讶、疑问是否符合脚本 | 安抚时过度兴奋 |
| 舒适与刺激控制 | 是否尖锐、突兀、突然很响 | 惊叫、爆音、瞬时大音量 |

客观审计：每个音频标记 `missing_word/repeated_word/pronunciation_error/abnormal_silence/clipping/noise/volume_jump`；人工核听是文本准确性的最终依据，ASR回转写只作为辅助。客观测峰值/削波、时长、文件损坏率、采样率；对输出统一转换至项目指定格式（如 PCM WAV 48kHz 16bit/mono，仅作为建议，实际按 Unity 播放项目配置冻结）。

### 8.2 工程及制作指标

| 指标 | 记录方法 |
|---|---|
| 首次安装成功及耗时 | 从全新环境到首个可播放WAV，记录人工介入 |
| 权重与依赖体积 | 磁盘实际占用、下载内容 |
| 资源占用 | 冷加载耗时、峰值显存、RAM、批处理耗时 |
| 一次可用率 | 无需重新生成即可通过录音准确/舒适度验收的条数/总条数 |
| 平均返工次数 | 合格版本前重生成/调参数的次数 |
| 角色配置成本 | 从零建立1种新声音所需试听和人工时间 |
| 音色来源风险 | 来源、授权、版本、是否涉及他人声音 |
| 断网可用性 | 全套权重离线后能否完成生成 |
| Unity素材播放 | 所有导出音频能否被 AudioClip 加载、正确中断/切换 |

### 8.3 加权分与硬门槛

建议权重：自然度+清晰度25%；角色匹配25%；音色一致20%；情绪控制15%；部署/制作成本15%。每项换算为0–100，权重计算前冻结归一化规则。

**硬性淘汰**：无法在实际制作环境运行；固定台词最终无法实现逐字准确（经有限返工后）；频繁刺耳/爆音且无法修正；无法合法使用权重或参考人声；三角色无法形成基本可辨性（若此项仍是正式需求）。如多个模型均过硬门槛，优先低返工与跨句稳定者，而不是单句最惊艳者。

## 9. 将本地 TTS 接入 Unity（推荐为音频素材，不是网络推理）

建议按 `speaker_id/line_id` 输出素材：

```text
Lumina/Assets/Audio/NPC/
├─ TEACHER/T01.wav
├─ TEACHER/T02.wav
├─ PEER_A/P01.wav
└─ PEER_B/P11.wav
```

统一维护 `dialogue_manifest.csv`：`line_id,role_id,text,emotion,model_id,voice_profile_id,source_audio_license,output_path,sample_rate,duration_ms,review_status,version,sha256`。Unity 通过 `line_id` 取 AudioClip；只有 `review_status=approved` 可发布。固定台词变更就重生成受影响文件，不需要全部重新合成。播放时验证中止、切换场景、重复播报、避免多个 NPC 音频叠加；不要把本地模型权重打进正式游戏包，除非后续需求明确改成运行时 TTS。

示例 Unity C# 使用 `Resources` 方式作原型（正式大项目可换 Addressables）：

```csharp
using UnityEngine;
public class NpcVoicePlayer : MonoBehaviour {
    public AudioSource audioSource;
    public void PlayApprovedLine(string speaker, string lineId) {
        // 原型路径：Assets/Resources/NPC/TEACHER/T01.wav
        var clip = Resources.Load<AudioClip>($"NPC/{speaker}/{lineId}");
        if (clip == null) { Debug.LogError("Missing approved clip: " + lineId); return; }
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
    }
}
```

注意示例使用 `Assets/Resources/NPC` 路径；若项目最终放在 `Assets/Audio/NPC`，需改成 Inspector 引用、Addressables 或先搬到 Resources；**不要误以为上述两种目录可直接兼容**。

## 10. 推荐目录、输出结果及验收清单

```text
tts_eval/
├─ config/voice_profiles.yaml      # 角色声音目标/候选模型参数
├─ data/lines_30.csv               # 30句固定评测文本
├─ adapters/qwen_voice_design.py   # 根据锁定版本的官方示例实现
├─ adapters/cosyvoice3.py
├─ adapters/melo_onnx.py
├─ scripts/generate_batch.py
├─ scripts/measure_audio.py
├─ scripts/build_blind_test.py
├─ scripts/aggregate_scores.py
├─ audio/raw/{model_id}/{role_id}/
├─ audio/approved/{role_id}/
├─ review/blind_map_private.csv
├─ review/listening_scores.csv
├─ results/generation_logs.csv
├─ results/model_comparison.csv
└─ reports/selection_decision.md
```

合格交付：三候选的安装/失败证据；全量270份候选音频或逐项失败日志；盲听评分表和逐字缺陷表；算力、返工成本与许可记录；最终首选/备选/淘汰原因；Unity 资源清单。**不得凭官方宣传代替本机合成试听，也不能声称本方案已验证儿童干预效果。**

## 11. 少量技术核对链接

- Qwen3-TTS 官方：https://github.com/QwenLM/Qwen3-TTS
- CosyVoice 官方示例：https://github.com/QwenAudio/CosyVoice/blob/main/example.py
- CosyVoice3 实际推理方法：https://github.com/QwenAudio/CosyVoice/blob/main/cosyvoice/cli/cosyvoice.py
- sherpa-onnx 中文模型：https://k2-fsa.github.io/sherpa/onnx/tts/pretrained_models/vits.html
