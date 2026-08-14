using CommunityOS.Knowledge.Domain.Events;
using CommunityOS.SharedKernel.Domain.Events;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace CommunityOS.Knowledge.Tests.Security;

public class PublishDiagnostic
{
    private sealed class GenericHandler<TEvent> : INotificationHandler<TEvent>
        where TEvent : IDomainEvent
    {
        public static int Count;
        public Task Handle(TEvent n, CancellationToken ct) { Count++; return Task.CompletedTask; }
    }

    [Fact]
    public async Task Open_generic_handler_receives_concrete_events()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Knowledge.Application.KnowledgeApplicationServiceExtensions).Assembly));
        services.AddScoped(typeof(INotificationHandler<>), typeof(GenericHandler<>));
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        IDomainEvent evt = new AnswerAddedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "ai");
        await mediator.Publish(evt);

        GenericHandler<AnswerAddedEvent>.Count.Should().Be(1);
    }
}