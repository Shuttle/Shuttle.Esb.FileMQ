using System.Threading.Tasks;
using NUnit.Framework;
using Shuttle.Esb.Tests;

namespace Shuttle.Esb.FileMQ.Tests
{
	public class FileOutboxTest : OutboxFixture
	{
		[Test]
		[TestCase(false)]
		[TestCase(true)]
		public async Task Should_be_able_handle_errors(bool isTransactionalEndpoint)
		{
			await TestOutboxSending(FileQueueFixture.GetServiceCollection(), "filemq://local/{0}", 1, isTransactionalEndpoint);
		}
	}
}