#!/bin/bash

curl https://openrouter.ai/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $OPENAI_API_KEY" \
  -d "{ \
  "model": "$OPENAI_MODEL", \
  "messages": [ \
    { \
      "role": "user", \
      "content": "What is the meaning of life?" \
    } \
  ] \
}"
