# Graph Report - .  (2026-06-19)

## Corpus Check
- Corpus is ~4,826 words - fits in a single context window. You may not need a graph.

## Summary
- 169 nodes · 214 edges · 21 communities (10 shown, 11 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_MTG Card Model & Validation|MTG Card Model & Validation]]
- [[_COMMUNITY_Scryfall API Client (GetManager)|Scryfall API Client (GetManager)]]
- [[_COMMUNITY_Command-Line Options|Command-Line Options]]
- [[_COMMUNITY_SingleDouble-Face Cards|Single/Double-Face Cards]]
- [[_COMMUNITY_SimpleLogger|SimpleLogger]]
- [[_COMMUNITY_JSON Card Converter|JSON Card Converter]]
- [[_COMMUNITY_Program Entry Point|Program Entry Point]]
- [[_COMMUNITY_Card Analyzer|Card Analyzer]]
- [[_COMMUNITY_Project Config & Dependencies|Project Config & Dependencies]]
- [[_COMMUNITY_Resources Designer|Resources Designer]]
- [[_COMMUNITY_Console Writer|Console Writer]]
- [[_COMMUNITY_String Extensions|String Extensions]]
- [[_COMMUNITY_BulkData|BulkData]]
- [[_COMMUNITY_BulkData Collection|BulkData Collection]]
- [[_COMMUNITY_Card Search Model|Card Search Model]]
- [[_COMMUNITY_JsonCard Model|JsonCard Model]]
- [[_COMMUNITY_JsonCardFace Model|JsonCardFace Model]]
- [[_COMMUNITY_Json Image URIs|Json Image URIs]]
- [[_COMMUNITY_Set Model|Set Model]]
- [[_COMMUNITY_Set Search Model|Set Search Model]]
- [[_COMMUNITY_Mode Enum|Mode Enum]]

## God Nodes (most connected - your core abstractions)
1. `Card` - 20 edges
2. `GetManager` - 15 edges
3. `SimpleLogger` - 13 edges
4. `Card` - 6 edges
5. `CardAnalyzer` - 6 edges
6. `CommandLineOptions` - 6 edges
7. `JsonCardConverter` - 5 edges
8. `Program` - 5 edges
9. `Image` - 4 edges
10. `DoubleFaceCard` - 4 edges

## Surprising Connections (you probably didn't know these)
- `CommandLineOptions` --implements--> `IAllOptions`  [EXTRACTED]
  ScatoloneDownloader/Options/CommandLineOptions.cs → ScatoloneDownloader/Options/IAllOptions.cs
- `CommandLineOptions` --implements--> `IDataOptions`  [EXTRACTED]
  ScatoloneDownloader/Options/CommandLineOptions.cs → ScatoloneDownloader/Options/IDataOptions.cs
- `CommandLineOptions` --implements--> `IFileOptions`  [EXTRACTED]
  ScatoloneDownloader/Options/CommandLineOptions.cs → ScatoloneDownloader/Options/IFileOptions.cs
- `CommandLineOptions` --implements--> `ISetOptions`  [EXTRACTED]
  ScatoloneDownloader/Options/CommandLineOptions.cs → ScatoloneDownloader/Options/ISetOptions.cs
- `CommandLineOptions` --implements--> `IYearOptions`  [EXTRACTED]
  ScatoloneDownloader/Options/CommandLineOptions.cs → ScatoloneDownloader/Options/IYearOptions.cs

## Import Cycles
- None detected.

## Communities (21 total, 11 thin omitted)

### Community 0 - "MTG Card Model & Validation"
Cohesion: 0.14
Nodes (10): Color, double, JsonCard, Card, ScatoloneDownloader.Mtg, Dictionary, GetManager, Image (+2 more)

### Community 1 - "Scryfall API Client (GetManager)"
Cohesion: 0.23
Nodes (9): DateTime, Card, Dictionary, JsonSerializerOptions, List, Stream, string, GetManager (+1 more)

### Community 2 - "Command-Line Options"
Cohesion: 0.11
Nodes (12): CommandLineOptions, ScatoloneDownloader.Options, IAllOptions, ScatoloneDownloader.Options, IDataOptions, ScatoloneDownloader.Options, IFileOptions, ScatoloneDownloader.Options (+4 more)

### Community 3 - "Single/Double-Face Cards"
Cohesion: 0.15
Nodes (10): Card, DoubleFaceCard, ScatoloneDownloader.Mtg, ScatoloneDownloader.Mtg, SingleFaceCard, GetManager, Image, Stream (+2 more)

### Community 4 - "SimpleLogger"
Cohesion: 0.23
Nodes (5): LogLevel, object, string, ScatoloneDownloader, SimpleLogger

### Community 5 - "JSON Card Converter"
Cohesion: 0.24
Nodes (8): JsonCardConverter, ScatoloneDownloader.Json.Cards, JsonConverter, Card, JsonSerializerOptions, Type, Utf8JsonReader, Utf8JsonWriter

### Community 6 - "Program Entry Point"
Cohesion: 0.24
Nodes (5): CommandLineOptions, List, Mode, Program, ScatoloneDownloader

### Community 7 - "Card Analyzer"
Cohesion: 0.39
Nodes (4): CardAnalyzer, ScatoloneDownloader.Mtg, Dictionary, List

### Community 8 - "Project Config & Dependencies"
Cohesion: 0.33
Nodes (4): net9.0, CommandLineParser (2.9.1), System.Drawing.Common (8.0.2), Microsoft.NET.Sdk

### Community 9 - "Resources Designer"
Cohesion: 0.40
Nodes (4): CultureInfo, Resources, ScatoloneDownloader.Properties, ResourceManager

## Knowledge Gaps
- **62 isolated node(s):** `ScatoloneDownloader`, `ScatoloneDownloader.Enums`, `ScatoloneDownloader.Extensions`, `ScatoloneDownloader`, `string` (+57 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **What connects `ScatoloneDownloader`, `ScatoloneDownloader.Enums`, `ScatoloneDownloader.Extensions` to the rest of the system?**
  _62 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `MTG Card Model & Validation` be split into smaller, more focused modules?**
  _Cohesion score 0.13675213675213677 - nodes in this community are weakly interconnected._
- **Should `Command-Line Options` be split into smaller, more focused modules?**
  _Cohesion score 0.1111111111111111 - nodes in this community are weakly interconnected._