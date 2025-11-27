# Product Overview

## Purpose
This project is an AI-powered agent system built on .NET that implements a robust background queuing mechanism for processing AI prompts. The system uses a producer-consumer pattern where agents can queue work items that are processed asynchronously by a hosted background service.

## Problems Solved
- **Asynchronous Processing**: Enables non-blocking execution of AI prompts through a background queuing system
- **Scalability**: Distributes workload efficiently using System.Threading.Channels for queue management
- **Resource Management**: Properly scopes dependency injection for each queued work item
- **Testability**: Provides a framework for comprehensive integration testing of the queuing architecture

## How It Works
The system consists of several key components:
1. **AgentService**: Acts as a producer that queues work items
2. **BackgroundTaskQueue**: Manages the queue using System.Threading.Channels
3. **QueuedHostedService**: Acts as a consumer that processes queued work items in background threads
4. **IAgent Interface**: Defines the contract for AI agent implementations

## User Experience Goals
- Seamless integration with existing .NET applications
- Reliable and efficient processing of AI workloads
- Clear separation of concerns between queuing and processing
- Comprehensive observability and error handling
- Easy-to-use API for submitting AI prompts