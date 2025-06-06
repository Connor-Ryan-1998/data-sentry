# Data Sentry

A comprehensive monitoring tool for data systems and services with alert integration.

## Table of Contents
- Overview
- Current Functionality
- Technical Details
- Configuration
- Development
- TODO List
- Known Issues
- Security Notes

## Overview

Data Sentry is a monitoring application designed to check the health and status of various data systems and services. It functions as a central dashboard for data pipeline monitoring, allowing users to define and run multiple checks against different platforms and generate alerts when issues are detected.

## Current Functionality

### Supported Systems
| System | Description |
|--------|-------------|
| **SQL Server** | Execute custom SQL queries against SQL Server databases |
| **Snowflake** | Run queries against Snowflake data warehouses |
| **Jira** | Search for issues using JQL (Jira Query Language) |
| **Azure Data Factory** | Monitor pipeline failures and Self-Hosted Integration Runtime status |
| **OpsGenie** | Send alerts to incident management platforms |

### Core Features
- **Configurable Checks**: Define checks in a JSON configuration file
- **Dashboard**: Visual overview of check statuses with color coding
- **Detailed Results**: View detailed JSON results for each check
- **Export**: Export check results to CSV or JSON files
- **Background Monitoring**: Run as a background service with system tray integration
- **Alert Integration**: Send failures to OpsGenie for incident management

## Technical Details

### Architecture
- Built with .NET using Avalonia UI for cross-platform support
- MVVM architecture (Models, Views, ViewModels)
- Uses CommunityToolkit.Mvvm for MVVM implementation
- Configuration-driven check definitions

### Key Components
1. **CheckRecord**: Represents an individual monitoring check
2. **ChecksViewModel**: Manages the collection of active checks
3. **SqlServerModel/SnowflakeModel/JiraModel/AzureDataFactoryModel**: Service-specific connectors
4. **ExternalNotificationModel**: Generic model for pushing alerts to external services
5. **DaemonService**: Background monitoring service

## Configuration

Checks are defined in a `config.json` file in the application's root directory. Each check requires:
- `sentry_type`: The system to check (sqlserver, snowflake, jira, adf)
- `description`: Human-readable description of the check
- Service-specific parameters (e.g., connection details, queries)

### Example Check Configuration

```json
[
  {
    "sentry_type": "sqlserver",
    "description": "Check Database Availability",
    "server": "sqlserver.example.com",
    "database": "master",
    "sql_query": "SELECT 'OK' AS status"
  },
  {
    "sentry_type": "jira",
    "description": "Check Critical Bugs",
    "server": "https://jira.example.com",
    "username": "username",
    "access_token": "your-api-key",
    "jql_query": "project = PROJ AND issuetype = Bug AND priority = Critical AND status != Closed"
  }
]
```

## Development

### Testing Locally
```bash
dotnet restore
dotnet run --property WarningLevel=0
```

### Building for Distribution
```bash
# For Windows x64
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

# Output location:
# bin\Release\net9.0\win-x64\publish\data-sentry.exe
```

### Local Development with Secrets (In development)
```bash
dotnet user-secrets init
dotnet user-secrets set "OpsGenie:ApiKey" "YourApiKeyHere"
```

### ADF Testing Prerequisites
Ensure Azure CLI is installed and logged in before testing ADF features.

## TODO List

### High Priority
- [ ] Implement authentication for ExternalNotificationModel using proper API key format
- [ ] Create templated alert messages for OpsGenie notifications
- [ ] Implement retry logic for failed connections
- [ ] Add logging/heartbeat for all operations and errors

### Medium Priority
- [ ] Create custom dashboard views (grouping by service type)
- [ ] Implement scheduled checks with configurable intervals
- [ ] Add support for more notification targets (email, Slack, Teams)
- [ ] Create user guide documentation
- [ ] Add check dependencies (chain checks together)

### Low Priority
- [ ] Create installer package for easier distribution
- [ ] Add unit tests for all models
- [ ] Add configuration checks wizard
- [ ] Support for additional data sources (MySQL, PostgreSQL, etc.)

## Known Issues
- OpsGenie integration needs proper API key format in headers
- ADF queries may time out on large factories
- Background service may consume excessive resources on some systems
- No proper validation on configuration parameters yet

## Security Notes
> **Important**: API keys and passwords are stored in plaintext in configuration files. Consider using Azure Key Vault or similar for production deployments, and use specific permissions for service accounts when possible.

