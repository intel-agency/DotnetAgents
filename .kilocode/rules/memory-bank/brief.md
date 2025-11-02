# Project Brief

This is an AI-powered agent system built on .NET that implements a robust background queuing mechanism for processing AI prompts. The system uses a producer-consumer pattern where agents can queue work items that are processed asynchronously by a hosted background service.

The primary objective is to enable non-blocking execution of AI operations while ensuring scalability and resource isolation. The system leverages .NET 8.0, ASP.NET Core, System.Threading.Channels, and IHostedService to create an efficient and testable architecture.

Key features include:
- Asynchronous processing of AI prompts through a background queuing system
- Scalable workload distribution using System.Threading.Channels
- Proper resource management through DI scoping for each queued work item
- Comprehensive integration testing framework using WebApplicationFactory

The system is designed for seamless integration with existing .NET applications, providing a reliable and efficient way to process AI workloads without blocking the main application thread.