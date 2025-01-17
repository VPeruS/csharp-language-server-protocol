using System;
using System.Collections.Generic;
using System.Reflection;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace OmniSharp.Extensions.LanguageServer.Server.Abstractions
{
    public interface IHandlerCollection : IEnumerable<ILspHandlerDescriptor>
    {
        LspHandlerDescriptorDisposable Add(params IJsonRpcHandler[] handlers);
        LspHandlerDescriptorDisposable Add(params Type[] handlerTypes);
        LspHandlerDescriptorDisposable Add(string method, IJsonRpcHandler handler);
        LspHandlerDescriptorDisposable Add(string method, Func<IServiceProvider, IJsonRpcHandler> handlerFunc);
        LspHandlerDescriptorDisposable Add(string method, Type handlerType);
        bool ContainsHandler(Type type);
        bool ContainsHandler(TypeInfo typeInfo);
        IEnumerable<ITextDocumentIdentifier> TextDocumentIdentifiers();
    }
}
