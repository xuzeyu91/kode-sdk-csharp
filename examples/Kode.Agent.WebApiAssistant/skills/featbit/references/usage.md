# FeatBit Skill Usage Guide

## Quick Start

### 1. Command Line Usage

```bash
# Query questions
python scripts/query.py "How to integrate .NET SDK"

# List all documentation pages
python scripts/query.py --list-pages

# JSON format output
python scripts/query.py "Where to find SDK key?" --json
```

### 2. Python API Usage

```python
from scripts.query import FeatBitAnswer

# Initialize
assistant = FeatBitAnswer()

# Query
result = assistant.ask("How to configure targeting rules?")

# Get pages to fetch
for page in result['pages']:
    print(f"Fetch: {page['url']}")
```

### 3. Complete Q&A Workflow

```python
from scripts.query import FeatBitAnswer
from mcp__glm-web-reader import webReader

# 1. Query to locate pages
assistant = FeatBitAnswer()
result = assistant.ask("How to integrate React SDK?")

# 2. Fetch page content
page_contents = []
for page in result['pages']:
    content = webReader(
        url=page['url'],
        return_format="markdown",
        retain_images=False
    )
    page_contents.append({
        "url": page['url'],
        "content": content
    })

# 3. Generate answer prompt
prompt = assistant.format_answer_prompt(
    question=result['question'],
    page_contents=page_contents
)

# 4. Call LLM to generate answer
# answer = llm_generate(prompt)
```

## Question Type Identification

### SDK Integration Questions

**Trigger Keywords**: SDK, integration, initialize, client, server

**Platform Identification**:
- .NET: "dotnet", ".net", "c#"
- JavaScript: "javascript", "js", "typescript", "ts"
- React: "react", "reactjs"
- Node.js: "node", "nodejs"
- Python: "python", "py"
- Go: "go", "golang"
- Java: "java", "kotlin"

**Example**:
```
Input: "How to integrate .NET SDK?"
Output: https://docs.featbit.co/docs/sdks/dotnet-server-sdk
```

### Deployment Operations Questions

**Trigger Keywords**: deployment, install, setup, docker, kubernetes

**Platform Identification**:
- Docker: "docker", "container"
- Kubernetes: "kubernetes", "k8s"
- Helm: "helm"
- Azure: "azure", "microsoft cloud"
- AWS: "aws", "amazon"

**Example**:
```
Input: "Docker deployment steps"
Output: https://docs.featbit.co/docs/deployment/docker-compose
```

### Feature Management Questions

**Trigger Keywords**: feature flag, toggle, targeting, rollout, A/B

**Type Identification**:
- Create Feature: "create", "new"
- Targeting Rules: "targeting", "rules"
- Rollout: "rollout", "gradual", "publish"
- A/B Testing: "ab test", "a/b", "experiment"

**Example**:
```
Input: "How to configure targeting rules?"
Output: https://docs.featbit.co/docs/feature-flags/targeting-rules
```

### Configuration Management Questions

**Trigger Keywords**: configuration, config, sdk key, secret, environment, webhook

**Example**:
```
Input: "Where to find SDK key?"
Output: https://docs.featbit.co/docs/configuration/sdk-key
```

## Fetching Documentation Content

### Available Methods

Choose the appropriate method based on your environment:

#### Method 1: MCP Tool (Simplest)

If you have `webReader` or similar MCP tools:

```python
# Fetch official documentation page
result = webReader(
    url="https://docs.featbit.co/installation/docker-compose",
    return_format="markdown",
    retain_images=False
)
```

#### Method 2: Python requests

```python
import requests
from bs4 import BeautifulSoup

# Fetch official documentation
url = "https://docs.featbit.co/installation/docker-compose"
response = requests.get(url, headers={'User-Agent': 'Mozilla/5.0'})
soup = BeautifulSoup(response.text, 'html.parser')

# Extract main content
content = soup.find('main').get_text()
```

#### Method 3: GitHub README (SDK)

```python
# SDK docs are on GitHub, fetch README directly
url = "https://raw.githubusercontent.com/featbit/featbit-js-client-sdk/main/README.md"
response = requests.get(url)
content = response.text
```

## Answer Format Requirements

### Standard Format

```markdown
## Answer

[Direct answer to user question, clear and concise]

### Code Example

```language
[Runnable code example]
```

---
**Source**: [Page Title](https://docs.featbit.co/docs/...)
```

### Special Cases Handling

1. **No answer in documentation**:
```
Sorry, the official documentation does not contain information about "xxx".
Please check [Official Documentation](https://docs.featbit.co/) or ask in [GitHub Issues](https://github.com/featbit/featbit/issues).
```

2. **Multiple relevant pages**:
```
Based on your question, the following documentation may be relevant:

1. [Page 1 Title](URL1) - [Brief description]
2. [Page 2 Title](URL2) - [Brief description]
```

## Troubleshooting

### Question Location Failure

**Symptom**: Returns default overview page

**Possible Causes**:
- Keywords not in mapping table
- Question phrasing too vague

**Solution**:
1. Check `references/keywords.md` to confirm keywords
2. Use `--list-pages` to view all available pages
3. Manually specify URL

### Fetch Failure

**Symptom**: webReader returns error

**Possible Causes**:
- Network issues
- Page doesn't exist (404)
- Server rate limiting

**Solution**:
1. Check network connection
2. Verify URL is correct
3. Add retry mechanism

## Extension Guide

### Adding New Pages

Edit `PAGES` dictionary in `scripts/query.py`:

```python
PAGES = {
    "sdk": {
        "new-platform": "https://docs.featbit.co/docs/sdks/new-platform-sdk",
    },
}
```

### Adding New Keywords

Edit `KEYWORDS` dictionary in `scripts/query.py`:

```python
KEYWORDS = {
    "new-platform": ["new-platform", "np"],
}
```
