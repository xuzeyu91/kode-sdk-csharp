#!/usr/bin/env python3
"""
FeatBit Documentation Query Assistant
Function: Locate relevant official documentation pages based on user questions and return fetch instructions
"""

from typing import List, Dict, Optional
from datetime import datetime


class FeatBitDocFinder:
    """FeatBit Documentation Page Locator"""

    # Documentation page mapping (organized by category)
    # Note: SDK docs are on GitHub, other docs are on docs.featbit.co
    PAGES = {
        # SDK Integration (GitHub repositories)
        "sdk": {
            "overview": "https://docs.featbit.co/sdk/overview",
            "javascript": "https://github.com/featbit/featbit-js-client-sdk",
            "react": "https://github.com/featbit/featbit-react-client-sdk",
            "react-native": "https://github.com/featbit/featbit-react-native-sdk",
            "node": "https://github.com/featbit/featbit-node-server-sdk",
            "dotnet-server": "https://github.com/featbit/featbit-dotnet-sdk",
            "dotnet-client": "https://github.com/featbit/featbit-dotnet-client-sdk",
            "java": "https://github.com/featbit/featbit-java-sdk",
            "python": "https://github.com/featbit/featbit-python-sdk",
            "go": "https://github.com/featbit/featbit-go-sdk",
        },
        # Deployment Installation
        "deployment": {
            "overview": "https://docs.featbit.co/installation/deployment-options",
            "docker": "https://docs.featbit.co/installation/docker-compose",
            "kubernetes": "https://github.com/featbit/featbit-charts",
            "helm": "https://github.com/featbit/featbit-charts",
            "azure": "https://github.com/featbit/azure-container-apps",
            "aws": "https://docs.featbit.co/installation/terraform-aws",
            "terraform": "https://docs.featbit.co/installation/terraform-aws",
            "own-infra": "https://docs.featbit.co/installation/use-your-own-infrastructure",
        },
        # Feature Management
        "features": {
            "overview": "https://docs.featbit.co/feature-flags/the-flag-list",
            "creating": "https://docs.featbit.co/getting-started/create-two-feature-flags",
            "targeting": "https://docs.featbit.co/feature-flags/targeting-users-with-flags/targeting-rules",
            "rollouts": "https://docs.featbit.co/feature-flags/targeting-users-with-flags/percentage-rollouts",
            "ab-testing": "https://docs.featbit.co/getting-started/how-to-guides/ab-testing",
            "variations": "https://docs.featbit.co/feature-flags/create-flag-variations",
        },
        # Configuration Management
        "config": {
            "environment": "https://docs.featbit.co/feature-flags/organizing-flags/environments",
            "sdk-key": "https://docs.featbit.co/getting-started/connect-an-sdk",
            "webhook": "https://docs.featbit.co/integrations/webhooks",
            "segment": "https://docs.featbit.co/feature-flags/users-and-user-segments/user-segments",
        },
        # Concepts
        "concepts": {
            "overview": "https://docs.featbit.co/",
            "getting-started": "https://docs.featbit.co/getting-started/connect-an-sdk",
        },
    }

    # Keyword mapping
    KEYWORDS = {
        # SDK Related
        "sdk": ["sdk", "client", "server", "integration", "integration", "initialize", "initialization"],
        "dotnet": ["dotnet", ".net", "c#", "csharp"],
        "javascript": ["javascript", "js", "typescript", "ts"],
        "react": ["react", "reactjs"],
        "node": ["node", "nodejs", "node.js"],
        "python": ["python", "py"],
        "go": ["go", "golang"],
        "java": ["java", "kotlin", "spring"],

        # Deployment Related
        "deployment": ["deployment", "deploy", "install", "installation", "setup"],
        "docker": ["docker", "container"],
        "kubernetes": ["kubernetes", "k8s", "k8"],
        "helm": ["helm"],
        "azure": ["azure", "microsoft cloud"],
        "aws": ["aws", "amazon"],

        # Feature Related
        "feature": ["feature flag", "feature toggle", "toggle", "flag", "feature"],
        "targeting": ["targeting", "rule", "rules"],
        "rollout": ["rollout", "gradual", "publish"],
        "ab-test": ["ab test", "a/b", "experiment"],

        # Configuration Related
        "config": ["configuration", "config", "setting", "settings"],
        "environment": ["environment", "env"],
        "sdk-key": ["sdk key", "secret", "key"],
        "webhook": ["webhook", "callback"],
    }

    @classmethod
    def find_pages(cls, question: str) -> List[Dict[str, str]]:
        """
        Find relevant documentation pages based on question

        Args:
            question: User question

        Returns:
            List of relevant pages, each containing {url, category, reason}
        """
        question_lower = question.lower()
        results = []

        # 1. Detect platform/technology keywords
        platform = None
        for key, keywords in cls.KEYWORDS.items():
            if any(kw in question_lower for kw in keywords):
                if key in ["dotnet", "javascript", "react", "node", "python", "go", "java"]:
                    platform = key
                    break

        # 2. Detect category
        category = None
        if any(kw in question_lower for kw in cls.KEYWORDS["deployment"]):
            category = "deployment"
        elif any(kw in question_lower for kw in cls.KEYWORDS["feature"]):
            category = "features"
        elif any(kw in question_lower for kw in cls.KEYWORDS["config"]):
            category = "config"
        elif any(kw in question_lower for kw in cls.KEYWORDS["sdk"]):
            category = "sdk"

        # 3. Combine results
        if category and category in cls.PAGES:
            if platform and platform in cls.PAGES[category]:
                # Exact match: platform + category
                results.append({
                    "url": cls.PAGES[category][platform],
                    "category": category,
                    "reason": f"Matched category: {category}, platform: {platform}"
                })
            else:
                # Category overview page
                if "overview" in cls.PAGES[category]:
                    results.append({
                        "url": cls.PAGES[category]["overview"],
                        "category": category,
                        "reason": f"Matched category: {category}"
                    })

        # 4. Fallback: if nothing found, return overview page
        if not results:
            results.append({
                "url": "https://docs.featbit.co/docs/getting-started",
                "category": "concepts",
                "reason": "Default overview page"
            })

        return results

    @classmethod
    def get_all_pages(cls) -> List[str]:
        """Get all documentation page URLs"""
        all_urls = []
        for category, pages in cls.PAGES.items():
            all_urls.extend(pages.values())
        return list(set(all_urls))


class FeatBitAnswer:
    """FeatBit Q&A Assistant"""

    def __init__(self):
        self.finder = FeatBitDocFinder()

    def ask(self, question: str) -> Dict:
        """
        Answer question

        Args:
            question: User question

        Returns:
            Answer dictionary containing question, pages, fetch_needed
        """
        # 1. Find relevant pages
        pages = self.finder.find_pages(question)

        # 2. Return fetch instructions
        return {
            "question": question,
            "pages": pages,
            "fetch_needed": True,
            "timestamp": datetime.now().isoformat()
        }

    def format_answer_prompt(self, question: str, page_contents: List[Dict]) -> str:
        """
        Format answer prompt

        Args:
            question: Original question
            page_contents: List of fetched page contents, each containing {url, content}

        Returns:
            Prompt for LLM

        Note: page_contents need to be fetched first using any method (webReader MCP / requests / browser)
        """
        prompt = f"""Please answer the question based on the following FeatBit official documentation content.

Question: {question}

Official Documentation Content:

"""
        for i, content in enumerate(page_contents, 1):
            prompt += f"\n## Document {i}: {content['url']}\n\n"
            prompt += f"{content['content']}\n\n"
            prompt += "---\n"

        prompt += """
Answer requirements:
1. Answer in English (or match question language)
2. Provide direct answer, do not repeat the question
3. If code is involved, provide runnable code examples
4. Must attach source links at the end of answer
5. If no answer in docs, say "information not found in official documentation"
"""
        return prompt


def main():
    """Command line entry"""
    import argparse
    import json

    parser = argparse.ArgumentParser(description="FeatBit Documentation Query")
    parser.add_argument("question", nargs="?", help="Your question")
    parser.add_argument("--list-pages", action="store_true", help="List all documentation pages")
    parser.add_argument("--json", action="store_true", help="Output in JSON format")

    args = parser.parse_args()

    if args.list_pages:
        pages = FeatBitDocFinder.get_all_pages()
        print(f"Total {len(pages)} documentation pages:\n")
        for url in sorted(pages):
            print(f"  {url}")
        return

    if not args.question:
        parser.print_help()
        return

    # Query
    assistant = FeatBitAnswer()
    result = assistant.ask(args.question)

    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print(f"\nQuestion: {result['question']}\n")
        print(f"Need to fetch {len(result['pages'])} pages:\n")

        for i, page in enumerate(result['pages'], 1):
            print(f"{i}. [{page['category']}] {page['url']}")
            print(f"   Reason: {page['reason']}\n")

        print("\nNext steps:")
        print("1. Use webReader MCP, requests, or browser to visit the above URLs")
        print("2. Call assistant.format_answer_prompt(question, contents) to generate answer")


if __name__ == "__main__":
    main()
