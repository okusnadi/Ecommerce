using System.Linq;
using MrCMS.Services;
using MrCMS.Web.Apps.CustomerFeedback.Entities;
using MrCMS.Web.Apps.CustomerFeedback.MessageTemplates;
using MrCMS.Web.Apps.CustomerFeedback.Settings;
using MrCMS.Website;
using NHibernate;

namespace MrCMS.Web.Apps.CustomerFeedback.Areas.Admin.Services
{
    public class SendFeedbackEmails : ISendFeedbackEmails
    {
        private readonly ISession _session;
        private readonly CustomerFeedbackSettings _settings;
        private readonly IMessageParser<CustomerFeedbackMessageTemplate, FeedbackRecord> _messageParser;

        public SendFeedbackEmails(ISession session, CustomerFeedbackSettings settings,
            IMessageParser<CustomerFeedbackMessageTemplate, FeedbackRecord> messageParser)
        {
            _session = session;
            _settings = settings;
            _messageParser = messageParser;
        }

        public void Send()
        {
            if (!_settings.IsEnabled)
                return;

            // Get Records that require message to be send to customer
            var feedbackToSend =
                _session.QueryOver<FeedbackRecord>()
                    .Where(
                        record =>
                            record.IsSent == false && (record.CreatedOn >= _settings.SendFeedbackStartDate) &&
                            (record.CreatedOn.AddDays(_settings.TimeAfterOrderToSendFeedbackEmail) >= CurrentRequestData.Now))
                    .Cacheable()
                    .List();

            if(!feedbackToSend.Any())
                return;

            // Queue Message and mark IsSent as true foreach record
            foreach (var feedbackRecord in feedbackToSend)
            {
                var queuedMessage = _messageParser.GetMessage(feedbackRecord);
                if (queuedMessage != null)
                {
                    _messageParser.QueueMessage(queuedMessage);
                    feedbackRecord.IsSent = true;
                    _session.Update(feedbackRecord);
                }
            }
        }
    }
}