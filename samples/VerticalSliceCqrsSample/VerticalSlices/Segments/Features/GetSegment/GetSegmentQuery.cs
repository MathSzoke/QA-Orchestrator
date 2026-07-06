using VerticalSliceCqrsSample.Shared.Kernel.CQRS;

namespace VerticalSliceCqrsSample.VerticalSlices.Segments.Features.GetSegment;

public sealed record GetSegmentQuery(Guid Id) : ICommand<GetSegmentResult>;

public sealed record GetSegmentResult(Guid Id, string Name);

public sealed class GetSegmentHandler : ICommandHandler<GetSegmentQuery, GetSegmentResult>
{
    public Task<GetSegmentResult> Handle(GetSegmentQuery command, CancellationToken cancellationToken)
        => Task.FromResult(new GetSegmentResult(command.Id, "Sample"));
}
