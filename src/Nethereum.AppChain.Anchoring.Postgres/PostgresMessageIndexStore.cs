using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nethereum.AppChain.Anchoring.Messaging;
using Nethereum.BlockchainProcessing.ProgressRepositories;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    public class PostgresMessageIndexStore : IMessageIndexStore
    {
        private readonly MessageIndexDbContext _context;

        public PostgresMessageIndexStore(MessageIndexDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task StoreAsync(MessageInfo message)
        {
            var chainId = (long)message.SourceChainId;
            var msgId = (long)message.MessageId;

            var existing = await _context.IndexedMessages
                .FirstOrDefaultAsync(m => m.SourceChainId == chainId && m.MessageId == msgId);

            if (existing == null)
            {
                _context.IndexedMessages.Add(ToEntity(message));
            }
            else
            {
                UpdateEntity(existing, message);
            }

            await _context.SaveChangesAsync();
        }

        public async Task StoreBatchAsync(IEnumerable<MessageInfo> messages)
        {
            foreach (var message in messages)
            {
                var chainId = (long)message.SourceChainId;
                var msgId = (long)message.MessageId;

                var existing = await _context.IndexedMessages
                    .FirstOrDefaultAsync(m => m.SourceChainId == chainId && m.MessageId == msgId);

                if (existing == null)
                {
                    _context.IndexedMessages.Add(ToEntity(message));
                }
                else
                {
                    UpdateEntity(existing, message);
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<MessageInfo?> GetAsync(ulong sourceChainId, ulong messageId)
        {
            var chainId = (long)sourceChainId;
            var msgId = (long)messageId;

            var entity = await _context.IndexedMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.SourceChainId == chainId && m.MessageId == msgId);

            return entity != null ? ToMessageInfo(entity) : null;
        }

        public async Task<List<MessageInfo>> GetPendingAsync(ulong sourceChainId, ulong afterMessageId, int maxCount)
        {
            var chainId = (long)sourceChainId;
            var afterId = (long)afterMessageId;

            var entities = await _context.IndexedMessages
                .AsNoTracking()
                .Where(m => m.SourceChainId == chainId && m.MessageId > afterId)
                .OrderBy(m => m.MessageId)
                .Take(maxCount)
                .ToListAsync();

            return entities.Select(ToMessageInfo).ToList();
        }

        public async Task<ulong> GetLastIndexedMessageIdAsync(ulong sourceChainId)
        {
            var chainId = (long)sourceChainId;

            var lastId = await _context.IndexedMessages
                .Where(m => m.SourceChainId == chainId)
                .MaxAsync(m => (long?)m.MessageId);

            return lastId.HasValue ? (ulong)lastId.Value : 0UL;
        }

        public async Task RemoveFromAsync(ulong sourceChainId, ulong messageId)
        {
            var chainId = (long)sourceChainId;
            var fromId = (long)messageId;

            var toRemove = await _context.IndexedMessages
                .Where(m => m.SourceChainId == chainId && m.MessageId >= fromId)
                .ToListAsync();

            _context.IndexedMessages.RemoveRange(toRemove);
            await _context.SaveChangesAsync();
        }

        public IBlockProgressRepository GetBlockProgressRepository(ulong sourceChainId)
        {
            return new MessageBlockProgressRepository(_context, (long)sourceChainId);
        }

        private static IndexedMessage ToEntity(MessageInfo message)
        {
            return new IndexedMessage
            {
                SourceChainId = (long)message.SourceChainId,
                MessageId = (long)message.MessageId,
                TargetChainId = (long)message.TargetChainId,
                Sender = message.Sender,
                Target = message.Target,
                Data = message.Data,
                BlockNumber = 0,
                Timestamp = message.Timestamp,
                IndexedAt = DateTime.UtcNow
            };
        }

        private static void UpdateEntity(IndexedMessage entity, MessageInfo message)
        {
            entity.TargetChainId = (long)message.TargetChainId;
            entity.Sender = message.Sender;
            entity.Target = message.Target;
            entity.Data = message.Data;
            entity.Timestamp = message.Timestamp;
        }

        private static MessageInfo ToMessageInfo(IndexedMessage entity)
        {
            return new MessageInfo
            {
                MessageId = (ulong)entity.MessageId,
                SourceChainId = (ulong)entity.SourceChainId,
                TargetChainId = (ulong)entity.TargetChainId,
                Sender = entity.Sender,
                Target = entity.Target,
                Data = entity.Data,
                Timestamp = entity.Timestamp
            };
        }
    }
}
