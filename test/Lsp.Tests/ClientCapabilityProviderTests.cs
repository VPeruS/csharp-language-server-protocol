using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using NSubstitute;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Server;
using Xunit;
using HandlerCollection = OmniSharp.Extensions.LanguageServer.Server.HandlerCollection;

namespace Lsp.Tests
{
    public class ClientCapabilityProviderTests
    {
        private static readonly Type[] Capabilities = typeof(ClientCapabilities).Assembly.GetTypes()
            .Where(x => x.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ConnectedCapability<>)))
            .ToArray();

        [Theory, MemberData(nameof(AllowSupportedCapabilities))]
        public void Should_AllowSupportedCapabilities(IJsonRpcHandler handler, object instance)
        {
            var textDocumentSyncHandler = TextDocumentSyncHandlerExtensions.With(DocumentSelector.ForPattern("**/*.cs"));

            var collection = new HandlerCollection(SupportedCapabilitiesFixture.AlwaysTrue) { textDocumentSyncHandler, handler };
            var provider = new ClientCapabilityProvider(collection);

            HasHandler(provider, instance).Should().BeTrue();
        }

        public static IEnumerable<object[]> AllowSupportedCapabilities()
        {
            return GetItems(Capabilities, type => {
                var handlerTypes = GetHandlerTypes(type);
                var handler = Substitute.For(handlerTypes.ToArray(), new object[0]);
                return new[] { handler, Activator.CreateInstance(typeof(Supports<>).MakeGenericType(type), true, Activator.CreateInstance(type)) };
            });
        }

        [Theory, MemberData(nameof(AllowUnsupportedCapabilities))]
        public void Should_AllowUnsupportedCapabilities(IJsonRpcHandler handler, object instance)
        {
            var textDocumentSyncHandler = TextDocumentSyncHandlerExtensions.With(DocumentSelector.ForPattern("**/*.cs"));

            var collection = new HandlerCollection(SupportedCapabilitiesFixture.AlwaysTrue) { textDocumentSyncHandler, handler };
            var provider = new ClientCapabilityProvider(collection);

            HasHandler(provider, instance).Should().BeTrue();
        }

        public static IEnumerable<object[]> AllowUnsupportedCapabilities()
        {
            return GetItems(Capabilities, type => {
                var handlerTypes = GetHandlerTypes(type);
                var handler = Substitute.For(handlerTypes.ToArray(), new object[0]);
                return new[] { handler, Activator.CreateInstance(typeof(Supports<>).MakeGenericType(type), false) };
            });
        }

        [Fact]
        public void Should_Invoke_Get_Delegate()
        {
            // Given
            var stub = Substitute.For<Func<IExecuteCommandOptions, ExecuteCommandOptions>>();
            var provider = new ClientCapabilityProviderFixture().GetStaticOptions();

            // When
            provider.Get(stub);

            // Then
            stub.Received().Invoke(Arg.Any<IExecuteCommandOptions>());
        }

        [Fact]
        public void Should_Invoke_Reduce_Delegate()
        {
            // Given
            var stub = Substitute.For<Func<IEnumerable<IExecuteCommandOptions>, ExecuteCommandOptions>>();
            var provider = new ClientCapabilityProviderFixture().GetStaticOptions();

            // When
            provider.Reduce(stub);

            // Then
            stub.Received().Invoke(Arg.Any<IEnumerable<IExecuteCommandOptions>>());
        }

        [Theory, MemberData(nameof(AllowNullSupportsCapabilities))]
        public void Should_AllowNullSupportedCapabilities(IJsonRpcHandler handler, object instance)
        {
            var textDocumentSyncHandler = TextDocumentSyncHandlerExtensions.With(DocumentSelector.ForPattern("**/*.cs"));

            var collection = new HandlerCollection(SupportedCapabilitiesFixture.AlwaysTrue) { textDocumentSyncHandler, handler };
            var provider = new ClientCapabilityProvider(collection);

            HasHandler(provider, instance).Should().BeTrue();
        }

        public static IEnumerable<object[]> AllowNullSupportsCapabilities()
        {
            return GetItems(Capabilities, type => {
                var handlerTypes = GetHandlerTypes(type);
                var handler = Substitute.For(handlerTypes.ToArray(), new object[0]);
                return new[] { handler, Activator.CreateInstance(typeof(Supports<>).MakeGenericType(type), true) };
            });
        }


        [Theory, MemberData(nameof(DisallowDynamicSupportsCapabilities))]
        public void Should_DisallowDynamicSupportedCapabilities(IJsonRpcHandler handler, object instance)
        {
            var textDocumentSyncHandler = TextDocumentSyncHandlerExtensions.With(DocumentSelector.ForPattern("**/*.cs"));

            var collection = new HandlerCollection(SupportedCapabilitiesFixture.AlwaysTrue) { textDocumentSyncHandler, handler };
            var provider = new ClientCapabilityProvider(collection);

            HasHandler(provider, instance).Should().BeFalse();
        }

        public static IEnumerable<object[]> DisallowDynamicSupportsCapabilities()
        {
            return GetItems(Capabilities, type => {
                var handlerTypes = GetHandlerTypes(type);
                var handler = Substitute.For(handlerTypes.ToArray(), new object[0]);
                var capability = Activator.CreateInstance(type);
                if (capability is DynamicCapability dyn) dyn.DynamicRegistration = true;
                return new[] { handler, Activator.CreateInstance(typeof(Supports<>).MakeGenericType(type), true, capability) };
            });
        }

        [Fact]
        public void Should_Handle_Mixed_Capabilities()
        {
            var textDocumentSyncHandler = TextDocumentSyncHandlerExtensions.With(DocumentSelector.ForPattern("**/*.cs"));

            var codeActionHandler = Substitute.For<ICodeActionHandler>();
            var definitionHandler = Substitute.For<IDefinitionHandler>();
            var typeDefinitionHandler = Substitute.For<ITypeDefinitionHandler>();

            var collection = new HandlerCollection(SupportedCapabilitiesFixture.AlwaysTrue) { textDocumentSyncHandler, codeActionHandler, definitionHandler, typeDefinitionHandler };
            var provider = new ClientCapabilityProvider(collection);
            var capabilities = new ClientCapabilities() {
                TextDocument = new TextDocumentClientCapabilities() {
                    CodeAction = new Supports<CodeActionCapability>(true, new CodeActionCapability() {
                        DynamicRegistration = false,
                    }),
                    TypeDefinition = new Supports<TypeDefinitionCapability>(true, new TypeDefinitionCapability() {
                        DynamicRegistration = true,
                    })
                }
            };

            provider.GetStaticOptions(capabilities.TextDocument.CodeAction).Get<ICodeActionOptions, CodeActionOptions>(CodeActionOptions.Of).Should().NotBeNull();
            provider.HasStaticHandler(capabilities.TextDocument.Definition).Should().BeTrue();
            provider.HasStaticHandler(capabilities.TextDocument.TypeDefinition).Should().BeFalse();
        }

        private static bool HasHandler(ClientCapabilityProvider provider, object instance)
        {
            return (bool)typeof(ClientCapabilityProviderTests).GetTypeInfo()
                .GetMethod(nameof(GenericHasHandler), BindingFlags.Static | BindingFlags.NonPublic)
                .MakeGenericMethod(instance.GetType().GetTypeInfo().GetGenericArguments()[0]).Invoke(null, new[] { provider, instance });
        }

        private static bool GenericHasHandler<T>(ClientCapabilityProvider provider, Supports<T> supports)
            where T : DynamicCapability, ConnectedCapability<IJsonRpcHandler>
        {
            return provider.HasStaticHandler(supports);
        }

        private static IEnumerable<object[]> GetItems<T>(IEnumerable<T> types, Func<T, IEnumerable<object>> func)
        {
            return types.Select(x => func(x).ToArray());
        }

        private static IEnumerable<Type> GetHandlerTypes(Type type)
        {
            return type.GetTypeInfo().ImplementedInterfaces
                .Where(x => x.GetTypeInfo().IsGenericType && x.GetTypeInfo().GetGenericTypeDefinition() == typeof(ConnectedCapability<>))
                .Select(x => x.GetTypeInfo().GetGenericArguments()[0]);
        }
    }
}
