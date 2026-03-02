# Architecture: Expense Management System on Azure

## Overview

This repository deploys a modern expense management application on Azure using Infrastructure-as-Code (Bicep), ASP.NET Core .NET 8, and Azure services.

## Azure Services Architecture

```mermaid
graph TD
    User(["👤 User / Browser"]) -->|HTTPS| AppSvc

    subgraph Azure_UKSouth["Azure — UK South"]
        AppSvc["🌐 Azure App Service\n(S1 Plan — .NET 8)\nRazor Pages + REST API"]
        SQL["🗄️ Azure SQL Database\n(Northwind — Basic Tier)\nAAD-only auth"]
        MID["🔑 User-Assigned\nManaged Identity\n(mid-appmodassist-prod)"]
        Search["🔍 Azure AI Search\n(S0 SKU)"]
    end

    subgraph Azure_SwedenCentral["Azure — Sweden Central"]
        AOAI["🤖 Azure OpenAI\n(GPT-4o model)\nCapacity: 8"]
    end

    AppSvc -->|"SQL connection\n(Managed Identity)"| SQL
    AppSvc -->|"OpenAI API\n(Managed Identity)"| AOAI
    AppSvc -->|"Search queries\n(Managed Identity)"| Search

    MID -.->|"Assigned to"| AppSvc
    MID -.->|"Cognitive Services\nOpenAI User role"| AOAI
    MID -.->|"Search Index\nData Reader role"| Search
    MID -.->|"db_datareader\ndb_datawriter\nEXECUTE"| SQL

    style MID fill:#f59e0b,color:#000
    style Azure_UKSouth fill:#eff6ff,stroke:#2563eb
    style Azure_SwedenCentral fill:#f0fdf4,stroke:#16a34a
```

## Component Descriptions

| Service | SKU | Region | Purpose |
|---------|-----|--------|---------|
| **App Service Plan** | S1 Standard | UK South | Hosts the .NET 8 web application (no cold starts) |
| **App Service** | — | UK South | Runs Razor Pages UI + REST API + Swagger |
| **Azure SQL** | Basic | UK South | Stores expenses, users, categories, statuses |
| **Azure OpenAI** | S0 / Standard | **Sweden Central** | GPT-4o model for AI assistant with function calling |
| **Azure AI Search** | S0 | UK South | Search index for RAG-based chat context |
| **Managed Identity** | User-assigned | UK South | Passwordless auth between all services |

## Security

- **No passwords or keys** stored anywhere — all connections use Managed Identity
- **AAD-only authentication** on SQL Server (MCAPS governance compliant)
- Managed Identity has **minimum required permissions** on each service
- App Service configured with **HTTPS-only**

## Deployment Flows

### Without GenAI (`bash deploy.sh`)
```
User → App Service → Azure SQL
```

### With GenAI (`bash deploy-with-chat.sh`)
```
User → App Service → Azure SQL
User → App Service → Azure OpenAI (function calling)
User → App Service → Azure AI Search
```

## Data Flow

1. **Browser** → makes HTTPS request to App Service
2. **Razor Pages** render the UI using data from API controllers
3. **API Controllers** call `IExpenseService` which executes **stored procedures** via Managed Identity connection to Azure SQL
4. **Chat API** calls `IChatService` which uses Azure OpenAI with **function calling** to interact with the expense data
5. **Managed Identity** authenticates all Azure service-to-service calls (no secrets needed)
