namespace VerticalSliceCqrsSample.Shared.Kernel.CQRS;

public interface ICommand;

public interface ICommand<out TResponse>;
