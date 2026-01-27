# FeatBit Keyword Recognition Rules

This document lists keyword rules used for question type identification.

**⚠️ Agent Note**: Must read this document completely to understand keyword matching logic and priorities.

---

## Keyword Categories

### SDK Related

| Keyword Group | Match Words | Identifies As | Priority |
|---------------|-------------|---------------|----------|
| sdk | sdk, client, server, integration, initialize, initialization | SDK Integration category | High |
| dotnet | dotnet, .net, c#, csharp | .NET platform | High |
| javascript | javascript, js, typescript, ts | JavaScript platform | High |
| react | react, reactjs | React platform | High |
| node | node, nodejs, node.js | Node.js platform | High |
| python | python, py | Python platform | High |
| go | go, golang | Go platform | High |
| java | java, kotlin, spring | Java platform | High |

### Deployment Related

| Keyword Group | Match Words | Identifies As | Priority |
|---------------|-------------|---------------|----------|
| deployment | deployment, deploy, install, installation, setup | Deployment Operations category | High |
| docker | docker, container | Docker deployment method | Medium |
| kubernetes | kubernetes, k8s, k8 | Kubernetes deployment method | Medium |
| helm | helm | Helm deployment method | Medium |
| azure | azure, microsoft cloud | Azure platform | Medium |
| aws | aws, amazon | AWS platform | Medium |

### Feature Related

| Keyword Group | Match Words | Identifies As | Priority |
|---------------|-------------|---------------|----------|
| feature | feature flag, feature toggle, toggle, flag, feature | Feature Management category | High |
| targeting | targeting, rule, rules | Targeting rules | Medium |
| rollout | rollout, gradual, publish | Percentage rollouts | Medium |
| ab-test | ab test, a/b, experiment | A/B testing | Medium |

### Configuration Related

| Keyword Group | Match Words | Identifies As | Priority |
|---------------|-------------|---------------|----------|
| config | configuration, config, setting, settings | Configuration Management category | High |
| environment | environment, env | Environment configuration | Medium |
| sdk-key | sdk key, secret, key | SDK Key | Medium |
| webhook | webhook, callback | Webhook | Medium |

---

## Matching Priorities

### 1. Platform-Specific Keywords (Highest Priority)

If platform keywords (dotnet, javascript, react, node, python, go, java) are detected, prioritize matching that platform's specific page.

**Example**:
```
Input: "dotnet sdk"
Match: .NET Server SDK page
Result: https://github.com/featbit/featbit-dotnet-sdk
```

### 2. Category Keywords (Medium Priority)

If category keywords (deployment, config, feature, etc.) are detected, match that category's overview page.

**Example**:
```
Input: "how to deploy"
Match: Deployment overview page
Result: https://docs.featbit.co/installation/deployment-options
```

### 3. Fallback Strategy (Lowest Priority)

If no keywords are matched, return the default Getting Started page.

**Example**:
```
Input: "what is featbit"
Match: Getting Started page
Result: https://docs.featbit.co/getting-started/connect-an-sdk
```

---

## Combination Matching Rules

### Platform + Category

When both platform and category keywords appear, prioritize platform-specific page matching.

| Input | Platform | Category | Match Result | URL |
|------|----------|----------|--------------|-----|
| ".net sdk initialization" | .NET | SDK | .NET Server SDK | GitHub |
| "react deployment" | React | Deployment | React SDK | GitHub |
| "docker deployment" | Docker | Deployment | Docker Compose | docs.featbit.co |

**Decision Logic**:
1. Detect platform keyword → Lock platform
2. Detect category keyword → Determine category
3. Platform + Category → Exact match
4. If no platform match → Return category overview page

### Multi-Category Matching

When multiple category keywords appear, match by priority:

**Priority Order**:
1. **SDK Integration** (Highest) - Involves specific platform integration
2. **Deployment Operations** - Involves infrastructure
3. **Feature Management** - Involves feature flag operations
4. **Configuration Management** (Lowest) - Involves configuration items

**Example**:
```
Input: "where to find sdk key when deploying?"
Analysis: Contains "deployment" and "sdk key"
Decision: deployment has higher priority → Match deployment overview
Result: https://docs.featbit.co/installation/deployment-options
```

---

## Documentation Location Decision

**Key Decision**: SDK question vs other question

| Decision Basis | Doc Location | Fetch Method |
|----------------|--------------|--------------|
| Contains SDK keywords | GitHub | webReader on GitHub page |
| Contains deployment/feature/config | docs.featbit.co | webReader on docs page |
| Unable to determine | docs.featbit.co overview | webReader on docs page |

**Why Important**:
- SDK docs are not on official site, fetching docs.featbit.co directly will fail
- GitHub README contains detailed integration steps and code examples
- Confusion leads to providing incorrect documentation links to users

---

## Common Question Mapping Examples

| User Question | Detected Platform | Detected Category | Matched Page | URL |
|---------------|-------------------|-------------------|--------------|-----|
| How to integrate .NET SDK? | .NET | SDK | .NET Server SDK | GitHub |
| Docker deployment steps | Docker | Deployment | Docker Compose | docs.featbit.co |
| How to configure targeting rules? | - | Feature | Targeting Rules | docs.featbit.co |
| Where to find SDK key? | - | Configuration | Connect SDK | docs.featbit.co |
| How to initialize React SDK? | React | SDK | React SDK | GitHub |
| K8s deployment minimum config | K8s | Deployment | Kubernetes | GitHub |
| How to do A/B testing? | - | Feature | A/B Testing | docs.featbit.co |
| How to configure users in Python SDK? | Python | SDK | Python SDK | GitHub |

---

## Matching Algorithm Explanation

### Implementation Logic (Pseudocode)

```python
def match_question(question):
    question_lower = question.lower()
    
    # 1. Detect platform
    platform = None
    for platform_name, keywords in PLATFORM_KEYWORDS.items():
        if any(kw in question_lower for kw in keywords):
            platform = platform_name
            break
    
    # 2. Detect category
    category = None
    for category_name, keywords in CATEGORY_KEYWORDS.items():
        if any(kw in question_lower for kw in keywords):
            category = category_name
            break
    
    # 3. Combine results
    if platform and category:
        # Exact match
        return PAGES[category][platform]
    elif category:
        # Category overview
        return PAGES[category]["overview"]
    else:
        # Default
        return DEFAULT_PAGE
```

---

## Extension Rules

### Adding New Keywords

Add to `KEYWORDS` dictionary in `scripts/query.py`:

```python
KEYWORDS = {
    "new-platform": ["new-platform", "np"],
}
```

**Important**: When modifying, also update:
1. `scripts/query.py` - Primary data source
2. `references/page-map.md` - Documentation
3. `references/keywords.md` - This document

### Adding Synonyms

Support Chinese-English mixing, recommend adding common abbreviations and variations.

**Example**:
```python
"dotnet": ["dotnet", ".net", "c#", "csharp", "microsoft"]
```

---

## Notes

1. **Case Insensitive**: All matching is processed in lowercase
2. **Partial Matching**: Keywords are substring matches ("docker" will match "docker-compose")
3. **Priority Order**: First matched keyword determines result
4. **Default Behavior**: Returns Getting Started page when no match

---

## Debugging Tips

### Test with query.py

```bash
# View match results
python scripts/query.py "your question"

# List all pages
python scripts/query.py --list-pages

# JSON output
python scripts/query.py "your question" --json
```

### View Detailed Match Information

```python
from scripts.query import FeatBitDocFinder

pages = FeatBitDocFinder.find_pages("How to initialize React SDK?")
for page in pages:
    print(f"URL: {page['url']}")
    print(f"Category: {page['category']}")
    print(f"Reason: {page['reason']}")
```

---

## Update History

- **2024-01**: Initial version, basic keyword rules
- **2025-01-XX**: Optimized version v4.0.0
  - Added priority explanations
  - Added documentation location decision rules
  - Added combination matching decision table
  - Added common question mapping examples
