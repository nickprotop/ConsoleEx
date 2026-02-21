namespace DotNetIDE;

// LSP 2.0 minimal types (zero-based positions)
public record LspPosition(int Line, int Character);
public record LspRange(LspPosition Start, LspPosition End);
public record LspLocation(string Uri, LspRange Range);

public record LspDiagnostic(
    LspRange Range,
    string Message,
    int Severity,   // 1=Error, 2=Warning, 3=Information, 4=Hint
    string? Code);

public record CompletionItem(
    string Label,
    string? Detail,
    string? InsertText,
    int Kind);   // 1=Text, 2=Method, 3=Function, 4=Constructor, 5=Field, 6=Variable, 7=Class, ...

public record HoverResult(string Contents);

// JSON-RPC types
public record JsonRpcRequest(string jsonrpc, int id, string method, object? @params);
public record JsonRpcNotification(string jsonrpc, string method, object? @params);
public record JsonRpcResponse(string jsonrpc, int id, object? result, object? error);
