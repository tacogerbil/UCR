# Target Architecture

The UCR application is transitioning from monolithic God Objects to a layered MCCC architecture:

1. **Persistence Layer**: Handles XML serialization and DTOs.
2. **Adapters**: Handles hardware I/O, threads, and timers.
3. **Services (Core Logic)**: Pure business logic orchestrating data flows.
4. **Facades**: Temporary bridges to maintain existing UI bindings.
