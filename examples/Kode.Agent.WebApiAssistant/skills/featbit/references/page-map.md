# FeatBit Documentation Page Mapping

This document lists all official documentation pages available for fetching.

**⚠️ Important**: SDK documentation is hosted on GitHub, other documentation is on docs.featbit.co

## Documentation Source Decision Rules

| Doc Type | Hosting | Fetch Method | Decision Keywords |
|----------|---------|--------------|-------------------|
| SDK Integration | GitHub README | webReader on GitHub repo | sdk, integration, initialize, client, server |
| Deployment Installation | docs.featbit.co | webReader on docs | deployment, docker, kubernetes, helm |
| Feature Management | docs.featbit.co | webReader on docs | feature flag, toggle, targeting, rollout, ab test |
| Configuration Management | docs.featbit.co | webReader on docs | sdk key, secret, environment, webhook |

**Decision Logic**: If question contains SDK-related keywords → GitHub; otherwise → docs.featbit.co

---

## SDK Integration (GitHub Repositories)

| Page | URL | Description | Platform Keywords |
|------|-----|-------------|-------------------|
| SDK Overview | https://docs.featbit.co/sdk/overview | Overview of all SDKs | - |
| JavaScript SDK | https://github.com/featbit/featbit-js-client-sdk | JavaScript/TypeScript client SDK | javascript, js, typescript, ts |
| React SDK | https://github.com/featbit/featbit-react-client-sdk | React client SDK | react, reactjs |
| React Native SDK | https://github.com/featbit/featbit-react-native-sdk | React Native SDK | react-native, react native |
| Node.js SDK | https://github.com/featbit/featbit-node-server-sdk | Node.js server SDK | node, nodejs, node.js |
| .NET Server SDK | https://github.com/featbit/featbit-dotnet-sdk | .NET server SDK | dotnet, .net, c#, csharp |
| .NET Client SDK | https://github.com/featbit/featbit-dotnet-client-sdk | .NET client SDK | dotnet, .net, c#, csharp |
| Java SDK | https://github.com/featbit/featbit-java-sdk | Java server SDK | java, kotlin, spring |
| Python SDK | https://github.com/featbit/featbit-python-sdk | Python server SDK | python, py |
| Go SDK | https://github.com/featbit/featbit-go-sdk | Go server SDK | go, golang |

---

## Deployment Installation

| Page | URL | Description | Platform Keywords |
|------|-----|-------------|-------------------|
| Deployment Options | https://docs.featbit.co/installation/deployment-options | Deployment methods overview | - |
| Docker Compose | https://docs.featbit.co/installation/docker-compose | Deploy using Docker Compose | docker, container |
| Kubernetes | https://github.com/featbit/featbit-charts | Deploy to K8s using Helm Chart | kubernetes, k8s, k8 |
| Helm | https://github.com/featbit/featbit-charts | Helm Chart (same as K8s) | helm |
| Azure | https://github.com/featbit/azure-container-apps | Deploy to Azure Container Apps | azure, microsoft cloud |
| AWS Terraform | https://docs.featbit.co/installation/terraform-aws | Deploy to AWS using Terraform | aws, amazon |
| Self-hosted Infrastructure | https://docs.featbit.co/installation/use-your-own-infrastructure | Use your own database and Redis | own infra, self-hosted |

---

## Feature Management

| Page | URL | Description | Feature Keywords |
|------|-----|-------------|------------------|
| Flag List | https://docs.featbit.co/feature-flags/the-flag-list | Feature flag list management | list |
| Create Feature Flags | https://docs.featbit.co/getting-started/create-two-feature-flags | Create new feature flags | create, new |
| Targeting Rules | https://docs.featbit.co/feature-flags/targeting-users-with-flags/targeting-rules | Configure user targeting rules | targeting, rules |
| Percentage Rollouts | https://docs.featbit.co/feature-flags/targeting-users-with-flags/percentage-rollouts | Roll out by percentage | rollout, gradual, publish |
| A/B Testing | https://docs.featbit.co/getting-started/how-to-guides/ab-testing | Configure A/B testing | ab test, a/b, experiment |
| Flag Variations | https://docs.featbit.co/feature-flags/create-flag-variations | Configure multi-variation flags | variations |

---

## Configuration Management

| Page | URL | Description | Config Keywords |
|------|-----|-------------|-----------------|
| Environment Management | https://docs.featbit.co/feature-flags/organizing-flags/environments | Manage multiple environments (Dev/Prod) | environment, env |
| Connect SDK | https://docs.featbit.co/getting-started/connect-an-sdk | SDK Key and environment secret config | sdk key, secret, connect |
| Webhooks | https://docs.featbit.co/integrations/webhooks | Configure webhook callbacks | webhook, callback |
| User Segments | https://docs.featbit.co/feature-flags/users-and-user-segments/user-segments | Configure user segments | segment |

---

## Concept Documentation

| Page | URL | Description |
|------|-----|-------------|
| Homepage Overview | https://docs.featbit.co/ | FeatBit documentation home |
| Getting Started | https://docs.featbit.co/getting-started/connect-an-sdk | Quick start guide |

---

## Page Category Statistics

| Category | Page Count | Hosting |
|----------|------------|---------|
| SDK Integration | 10 | GitHub |
| Deployment Installation | 7 | docs.featbit.co / GitHub |
| Feature Management | 6 | docs.featbit.co |
| Configuration Management | 4 | docs.featbit.co |
| Concept Documentation | 2 | docs.featbit.co |
| **Total** | **29** | - |

---

## URL Rules

### GitHub Repositories (SDK)

SDK documentation is hosted on GitHub, README files contain detailed usage instructions:

- **JavaScript**: featbit-js-client-sdk
- **React**: featbit-react-client-sdk
- **React Native**: featbit-react-native-sdk
- **Node.js**: featbit-node-server-sdk
- **.NET Server**: featbit-dotnet-sdk
- **.NET Client**: featbit-dotnet-client-sdk
- **Java**: featbit-java-sdk
- **Python**: featbit-python-sdk
- **Go**: featbit-go-sdk

### Official Documentation Site (Others)

Other documentation uses standard documentation paths:

```
https://docs.featbit.co/{category}/{page}
```

Examples:
- Deployment: `/installation/docker-compose`
- Features: `/feature-flags/targeting-users-with-flags/targeting-rules`
- Configuration: `/getting-started/connect-an-sdk`

---

## Fetching Recommendations

### GitHub README

For SDK-related questions, use webReader to fetch GitHub README:

```python
webReader(
    url="https://github.com/featbit/featbit-js-client-sdk",
    return_format="markdown"
)
```

**Note**: GitHub pages may contain additional elements, prioritize extracting README portion.

### Official Documentation

For other questions, fetch documentation pages directly:

```python
webReader(
    url="https://docs.featbit.co/installation/docker-compose",
    return_format="markdown"
)
```

---

## Update History

- **2024-01**: Updated links according to official documentation structure
- SDK docs unified to point to GitHub repositories
- Deployment docs path changed from `/deployment` to `/installation`
- **2025-01-XX**: Optimized version v4.0.0, added documentation source decision rules and decision logic table
