using LLMUnity;
using System.Collections.Generic;
using NUnit.Framework.Internal;
using NUnit.Framework;

namespace LLMUnityTests
{
    public class TestChatTemplate
    {
        List<ChatMessage> messages = new List<ChatMessage>()
        {
            new ChatMessage {role = "system", content = "you are a bot"},
            new ChatMessage {role = "user", content = "Hello, how are you?"},
            new ChatMessage {role = "assistant", content = "I'm doing great. How can I help you today?"},
            new ChatMessage {role = "user", content = "I'd like to show off how chat templating works!"},
            new ChatMessage {role = "assistant", content = "chat template is awesome"},
            new ChatMessage {role = "user", content = "do you think so?"},
        };

        [Test]
        public void TestChatML()
        {
            Assert.AreEqual(
                new ChatMLTemplate().ComputePrompt(messages, "user", "assistant"),
                "<|im_start|>system\nyou are a bot<|im_end|>\n<|im_start|>user\nHello, how are you?<|im_end|>\n<|im_start|>assistant\nI'm doing great. How can I help you today?<|im_end|>\n<|im_start|>user\nI'd like to show off how chat templating works!<|im_end|>\n<|im_start|>assistant\nchat template is awesome<|im_end|>\n<|im_start|>user\ndo you think so?<|im_end|>\n<|im_start|>assistant\n"
            );
        }

        [Test]
        public void TestGemma()
        {
            Assert.AreEqual(
                new GemmaTemplate().ComputePrompt(messages, "user", "assistant"),
                "<start_of_turn>user\nyou are a bot\n\nHello, how are you?<end_of_turn>\n<start_of_turn>model\nI'm doing great. How can I help you today?<end_of_turn>\n<start_of_turn>user\nI'd like to show off how chat templating works!<end_of_turn>\n<start_of_turn>model\nchat template is awesome<end_of_turn>\n<start_of_turn>user\ndo you think so?<end_of_turn>\n<start_of_turn>model\n"
            );
        }

        [Test]
        public void TestMistralInstruct()
        {
            Assert.AreEqual(
                new MistralInstructTemplate().ComputePrompt(messages, "user", "assistant"),
                "[INST] you are a bot\n\nHello, how are you? [/INST]I'm doing great. How can I help you today?</s>[INST] I'd like to show off how chat templating works! [/INST]chat template is awesome</s>[INST] do you think so? [/INST]"
            );
        }

        [Test]
        public void TestMistralChat()
        {
            Assert.AreEqual(
                new MistralChatTemplate().ComputePrompt(messages, "user", "assistant"),
                "[INST] you are a bot\n\n### user: Hello, how are you? [/INST]### assistant: I'm doing great. How can I help you today?</s>[INST] ### user: I'd like to show off how chat templating works! [/INST]### assistant: chat template is awesome</s>[INST] ### user: do you think so? [/INST]### assistant:"
            );
        }

        [Test]
        public void TestLLama2()
        {
            Assert.AreEqual(
                new LLama2Template().ComputePrompt(messages, "user", "assistant"),
                "<s>[INST] <<SYS>>\nyou are a bot\n<</SYS>> Hello, how are you? [/INST]I'm doing great. How can I help you today? </s><s>[INST] I'd like to show off how chat templating works! [/INST]chat template is awesome </s><s>[INST] do you think so? [/INST]"
            );
        }

        [Test]
        public void TestLLama2Chat()
        {
            Assert.AreEqual(
                new LLama2ChatTemplate().ComputePrompt(messages, "user", "assistant"),
                "<s>[INST] <<SYS>>\nyou are a bot\n<</SYS>> ### user: Hello, how are you? [/INST]### assistant: I'm doing great. How can I help you today? </s><s>[INST] ### user: I'd like to show off how chat templating works! [/INST]### assistant: chat template is awesome </s><s>[INST] ### user: do you think so? [/INST]### assistant:"
            );
        }

        [Test]
        public void TestLLama3Chat()
        {
            Assert.AreEqual(
                new LLama3ChatTemplate().ComputePrompt(messages, "user", "assistant"),
                "<|start_header_id|>system<|end_header_id|>\n\nyou are a bot<|eot_id|><|start_header_id|>user<|end_header_id|>\n\nHello, how are you?<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\nI'm doing great. How can I help you today?<|eot_id|><|start_header_id|>user<|end_header_id|>\n\nI'd like to show off how chat templating works!<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\nchat template is awesome<|eot_id|><|start_header_id|>user<|end_header_id|>\n\ndo you think so?<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n"
            );
        }

        [Test]
        public void TestAlpaca()
        {
            Assert.AreEqual(
                new AlpacaTemplate().ComputePrompt(messages, "user", "assistant"),
                "you are a bot\n\n### user: Hello, how are you?\n### assistant: I'm doing great. How can I help you today?\n### user: I'd like to show off how chat templating works!\n### assistant: chat template is awesome\n### user: do you think so?\n### assistant:"
            );
        }

        [Test]
        public void TestVicuna()
        {
            Assert.AreEqual(
                new VicunaTemplate().ComputePrompt(messages, "user", "assistant"),
                "you are a bot\n\nuser: Hello, how are you?\nassistant: I'm doing great. How can I help you today?\nuser: I'd like to show off how chat templating works!\nassistant: chat template is awesome\nuser: do you think so?\nassistant:"
            );
        }

        [Test]
        public void TestPhi2()
        {
            Assert.AreEqual(
                new Phi2Template().ComputePrompt(messages, "user", "assistant"),
                "you are a bot\n\nuser: Hello, how are you?\nassistant: I'm doing great. How can I help you today?\nuser: I'd like to show off how chat templating works!\nassistant: chat template is awesome\nuser: do you think so?\nassistant:"
            );
        }

        [Test]
        public void TestPhi3()
        {
            Assert.AreEqual(
                new Phi3Template().ComputePrompt(messages, "user", "assistant"),
                "<|user|>\nyou are a bot\n\nHello, how are you?<|end|>\n<|assistant|>\nI'm doing great. How can I help you today?<|end|>\n<|user|>\nI'd like to show off how chat templating works!<|end|>\n<|assistant|>\nchat template is awesome<|end|>\n<|user|>\ndo you think so?<|end|>\n<|assistant|>\n"
            );
        }

        [Test]
        public void TestPhi3_5()
        {
            Assert.AreEqual(
                new Phi3_5Template().ComputePrompt(messages, "user", "assistant"),
                "<|system|>\nyou are a bot<|end|>\n<|user|>\nHello, how are you?<|end|>\n<|assistant|>\nI'm doing great. How can I help you today?<|end|>\n<|user|>\nI'd like to show off how chat templating works!<|end|>\n<|assistant|>\nchat template is awesome<|end|>\n<|user|>\ndo you think so?<|end|>\n<|assistant|>\n"
            );
        }

        [Test]
        public void TestPhi4Mini()
        {
            Assert.AreEqual(
                new Phi4MiniTemplate().ComputePrompt(messages, "user", "assistant"),
                "<|system|>you are a bot<|end|><|user|>Hello, how are you?<|end|><|assistant|>I'm doing great. How can I help you today?<|end|><|user|>I'd like to show off how chat templating works!<|end|><|assistant|>chat template is awesome<|end|><|user|>do you think so?<|end|><|assistant|>"
            );
        }

        [Test]
        public void TestPhi4()
        {
            Assert.AreEqual(
                new Phi4Template().ComputePrompt(messages, "user", "assistant"),
                "<|im_start|>system<|im_sep|>you are a bot<|im_end|><|im_start|>user<|im_sep|>Hello, how are you?<|im_end|><|im_start|>assistant<|im_sep|>I'm doing great. How can I help you today?<|im_end|><|im_start|>user<|im_sep|>I'd like to show off how chat templating works!<|im_end|><|im_start|>assistant<|im_sep|>chat template is awesome<|im_end|><|im_start|>user<|im_sep|>do you think so?<|im_end|><|im_start|>assistant<|im_sep|>"
            );
        }

        [Test]
        public void TestZephyr()
        {
            Assert.AreEqual(
                new ZephyrTemplate().ComputePrompt(messages, "user", "assistant"),
                "<|system|>\nyou are a bot</s>\n<|user|>\nHello, how are you?</s>\n<|assistant|>\nI'm doing great. How can I help you today?</s>\n<|user|>\nI'd like to show off how chat templating works!</s>\n<|assistant|>\nchat template is awesome</s>\n<|user|>\ndo you think so?</s>\n<|assistant|>\n"
            );
        }

        [Test]
        public void TestDeepSeekV2()
        {
            Assert.AreEqual(
                new DeepSeekV2Template().ComputePrompt(messages, "user", "assistant"),
                "<｜begin▁of▁sentence｜>you are a bot\n\nUser: Hello, how are you?\n\nAssistant: I'm doing great. How can I help you today?<｜end▁of▁sentence｜>User: I'd like to show off how chat templating works!\n\nAssistant: chat template is awesome<｜end▁of▁sentence｜>User: do you think so?\n\nAssistant:"
            );
        }

        [Test]
        public void TestDeepSeekV3()
        {
            Assert.AreEqual(
                new DeepSeekV3Template().ComputePrompt(messages, "user", "assistant"),
                "<｜begin▁of▁sentence｜>you are a bot\n\n<｜User｜>Hello, how are you?<｜Assistant｜>I'm doing great. How can I help you today?<｜end▁of▁sentence｜><｜User｜>I'd like to show off how chat templating works!<｜Assistant｜>chat template is awesome<｜end▁of▁sentence｜><｜User｜>do you think so?<｜Assistant｜>"
            );
        }

        [Test]
        public void TestDeepSeekR1()
        {
            Assert.AreEqual(
                new DeepSeekR1Template().ComputePrompt(messages, "user", "assistant"),
                "<｜begin▁of▁sentence｜>you are a bot\n\n<｜User｜>Hello, how are you?<｜Assistant｜>I'm doing great. How can I help you today?<｜end▁of▁sentence｜><｜User｜>I'd like to show off how chat templating works!<｜Assistant｜>chat template is awesome<｜end▁of▁sentence｜><｜User｜>do you think so?<｜Assistant｜><think>\n\n</think>\n\n"
            );
        }

        [Test]
        public void TestQwen3()
        {
            Assert.AreEqual(
                new Qwen3Template().ComputePrompt(messages, "user", "assistant"),
                "<|im_start|>system\nyou are a bot<|im_end|>\n<|im_start|>user\nHello, how are you?<|im_end|>\n<|im_start|>assistant\nI'm doing great. How can I help you today?<|im_end|>\n<|im_start|>user\nI'd like to show off how chat templating works!<|im_end|>\n<|im_start|>assistant\nchat template is awesome<|im_end|>\n<|im_start|>user\ndo you think so?<|im_end|>\n<|im_start|>assistant\n<think>\n\n</think>\n\n"
            );
        }

        [Test]
        public void TestBitNet()
        {
            Assert.AreEqual(
                new BitNetTemplate().ComputePrompt(messages, "user", "assistant"),
                "System: you are a bot<|eot_id|>User: Hello, how are you?<|eot_id|>Assistant: I'm doing great. How can I help you today?<|eot_id|>User: I'd like to show off how chat templating works!<|eot_id|>Assistant: chat template is awesome<|eot_id|>User: do you think so?<|eot_id|>Assistant: "
            );
        }
    }
}
