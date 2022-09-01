using Microsoft.Extensions.Options;

namespace Shuttle.Esb.FileMQ
{
    public class FileQueueOptionsValidator : IValidateOptions<FileQueueOptions>
    {
        public ValidateOptionsResult Validate(string name, FileQueueOptions options)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return ValidateOptionsResult.Fail(Esb.Resources.QueueConfigurationNameException);
            }

            if (string.IsNullOrWhiteSpace(options.Path))
            {
                return ValidateOptionsResult.Fail(string.Format(Esb.Resources.QueueConfigurationItemException, name, nameof(options.Path)));
            }

            return ValidateOptionsResult.Success;
        }
    }
}