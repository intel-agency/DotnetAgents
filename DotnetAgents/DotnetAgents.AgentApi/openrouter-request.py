import requests
import json
import os

openai_endpoint = os.environ.get("OPENAPI_ENDPOINT")
#url = "https://openrouter.ai/api/v1/chat/completions"
url = f"{openai_endpoint}/chat/completions"
headers = {
    "Authorization": f"Bearer {os.environ.get('OPENAI_API_KEY')}",
    "Content-Type": "application/json"
}

kimi_linear_model = "moonshotai/kimi-linear-48b-a3b-instruct"
kimi_thinking_model = "moonshotai/kimi-k2-thinking"
qwen_model = "qwen/qwen3-235b-a22b-2507"
model = os.getenv("OPENAI_MODEL_NAME")

payload = {
    "models": [
        model
    ],
    "messages": [
    {
        "role": "system",
        "content": "You are an expert programmer in .NET and C#."
    },
    {
        "role": "user",
        "content": "If you built the world's tallest skyscraper, what would you name it?"
    }
    ],    
}

stream_response = True

payload["stream"] = stream_response

if stream_response:
     with requests.post(url, headers=headers, json=payload, stream=True) as resp:
        resp.raise_for_status()
        complete = ""
        for line in resp.iter_lines(decode_unicode=True):
            if not line or not line.startswith("data:"):
                continue
            data = line.removeprefix("data: ").strip()
            if data == "[DONE]":
                break
            chunk = json.loads(data)
            delta = chunk["choices"][0]["delta"].get("content")
            if delta:
                complete += delta
                print(delta, end="", flush=True)
        print()  # finish with a newline
        complete += "\n"
        print(f"Complete: {complete}")
else:
    response = requests.post(url, headers=headers, json=payload)
    try:
        print(response.json())
    except:
        print(response.text)
