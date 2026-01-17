using Kode.Agent.Examples;
using Kode.Agent.Examples.Shared;

// Load environment variables
EnvLoader.Load();

void ShowMenu()
{
    Console.WriteLine("Kode.Agent SDK Examples\n");
    Console.WriteLine("=== Agent Examples (require API key) ===");
    Console.WriteLine("  1. Getting Started - Simple agent interaction");
    Console.WriteLine("  2. Agent Inbox - Event streaming and tool monitoring");
    Console.WriteLine("  3. Approval Control - Permission and approval flow");
    Console.WriteLine("  4. Room Collab - Multi-agent collaboration");
    Console.WriteLine(" 10. Scheduler Watch - Scheduler + todo + file watching (TS-aligned)");
    Console.WriteLine();
    Console.WriteLine("=== SDK Feature Demos (no API key needed) ===");
    Console.WriteLine("  5. Scheduler & TimeBridge - Task scheduling");
    Console.WriteLine("  6. Hooks - Intercept agent behavior");
    Console.WriteLine("  7. Templates - Agent template registry");
    Console.WriteLine("  8. EventBus - Event-driven architecture");
    Console.WriteLine("  9. Custom Tools - Create your own tools");
    Console.WriteLine();
    Console.WriteLine("  q. Quit\n");
}

ShowMenu();

while (true)
{
    Console.Write("Enter choice (1-10, q): ");
    var choice = Console.ReadLine()?.Trim().ToLowerInvariant();

    try
    {
        switch (choice)
        {
            case "1":
                await GettingStarted.RunAsync();
                break;

            case "2":
                await AgentInbox.RunAsync();
                break;

            case "3":
                await ApprovalControl.RunAsync();
                break;  

            case "4":
                await RoomCollab.RunAsync();
                break;

            case "10":
                await SchedulerWatch.RunAsync();
                break;

            case "5":
                await SchedulerUsage.RunAsync();
                break;

            case "6":
                await HooksUsage.RunAsync();
                break;

            case "7":
                await TemplateUsage.RunAsync();
                break;

            case "8":
                await EventBusUsage.RunAsync();
                break;

            case "9":
                await CustomToolsExample.RunAsync();
                break;

            case "q":
            case "quit":
            case "exit":
                Console.WriteLine("Goodbye!");
                return;

            default:
                Console.WriteLine("Invalid choice. Please enter 1-10 or q to quit.\n");
                continue;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[error] {ex.Message}");
        Console.WriteLine($"[stacktrace] {ex.StackTrace}\n");
    }

    Console.WriteLine("\nPress any key to continue...");
    Console.ReadKey(true);
    Console.Clear();
    ShowMenu();
}
