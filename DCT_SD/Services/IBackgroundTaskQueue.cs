namespace DCT_SD.Services;

// A fire-and-forget work queue for anything that must run AFTER an HTTP response has already
// been sent back to the browser - e.g. the user registration/status-change emails in
// UsersController, whose synchronous SMTP send used to block the Save button's response for as
// long as the mail server took to answer (up to SmtpClient's own default 100s timeout against a
// slow/unreachable host). QueuedTaskHostedService drains this on a background thread, in its own
// DI scope - never the HTTP request's own scope, which is disposed the moment the controller
// action returns.
public interface IBackgroundTaskQueue
{
    void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem);

    Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}
