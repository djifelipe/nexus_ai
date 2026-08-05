## ADDED Requirements

### Requirement: Catalog-constrained required tools
The intent router SHALL populate `RequiredTools` for `DataQuery`, `PermissionCheck`, and workflow-related intents only with exact names present in the enabled read-only tool catalog. This routing hint MUST NOT bypass executor schema validation or authorization.

#### Scenario: Inventory balance intent is recognized
- **WHEN** deterministic rules uniquely classify an authorized question as an inventory balance `DataQuery`
- **THEN** `RequiredTools` contains `inventory.getBalance` and no invented tool name

#### Scenario: Intent remains ambiguous
- **WHEN** the router cannot determine which entity or registered tool applies
- **THEN** it requests clarification and does not emit a speculative required tool

#### Scenario: Catalog tool is disabled
- **WHEN** a rule maps to a tool that is not enabled for the current environment or tenant
- **THEN** the router does not advertise that tool and preserves controlled knowledge-only or insufficient-capability behavior

