using Microsoft.EntityFrameworkCore;
using Nethereum.DataServices.Sourcify.Database;
using Nethereum.DataServices.Sourcify.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nethereum.Sourcify.Database
{
    public class EFCoreSourcifyRepository : ISourcifyRepository
    {
        private readonly SourcifyDbContext _context;

        public EFCoreSourcifyRepository(SourcifyDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Code> GetCodeAsync(byte[] codeHash)
        {
            return await _context.Codes.FindAsync(codeHash);
        }

        public async Task AddCodeAsync(Code code)
        {
            var existing = await _context.Codes.FindAsync(code.CodeHash);
            if (existing == null)
            {
                _context.Codes.Add(code);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Contract> GetContractAsync(Guid id)
        {
            return await _context.Contracts.FindAsync(id);
        }

        public async Task AddContractAsync(Contract contract)
        {
            var existing = await _context.Contracts.FindAsync(contract.Id);
            if (existing == null)
            {
                _context.Contracts.Add(contract);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<ContractDeployment> GetDeploymentAsync(long chainId, byte[] address)
        {
            return await _context.ContractDeployments
                .FirstOrDefaultAsync(d => d.ChainId == chainId && d.Address.SequenceEqual(address));
        }

        public async Task AddDeploymentAsync(ContractDeployment deployment)
        {
            var existing = await GetDeploymentAsync(deployment.ChainId, deployment.Address);
            if (existing == null)
            {
                _context.ContractDeployments.Add(deployment);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<CompiledContract> GetCompiledContractAsync(Guid id)
        {
            return await _context.CompiledContracts.FindAsync(id);
        }

        public async Task AddCompiledContractAsync(CompiledContract compiledContract)
        {
            var existing = await _context.CompiledContracts.FindAsync(compiledContract.Id);
            if (existing == null)
            {
                _context.CompiledContracts.Add(compiledContract);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<VerifiedContract> GetVerifiedContractAsync(long chainId, byte[] address)
        {
            var deployment = await GetDeploymentAsync(chainId, address);
            if (deployment == null) return null;

            return await _context.VerifiedContracts
                .FirstOrDefaultAsync(v => v.DeploymentId == deployment.Id);
        }

        public async Task AddVerifiedContractAsync(VerifiedContract verifiedContract)
        {
            var existing = await _context.VerifiedContracts.FindAsync(verifiedContract.Id);
            if (existing == null)
            {
                _context.VerifiedContracts.Add(verifiedContract);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Signature> GetSignatureByHash4Async(byte[] hash4)
        {
            return await _context.Signatures
                .FirstOrDefaultAsync(s => s.SignatureHash4.SequenceEqual(hash4));
        }

        public async Task<Signature> GetSignatureByHash32Async(byte[] hash32)
        {
            return await _context.Signatures.FindAsync(hash32);
        }

        public async Task<List<Signature>> SearchSignaturesAsync(string query)
        {
            return await _context.Signatures
                .Where(s => s.SignatureText.Contains(query))
                .Take(100)
                .ToListAsync();
        }

        public async Task AddSignatureAsync(Signature signature)
        {
            var existing = await _context.Signatures.FindAsync(signature.SignatureHash32);
            if (existing == null)
            {
                _context.Signatures.Add(signature);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Source> GetSourceAsync(byte[] sourceHash)
        {
            return await _context.Sources.FindAsync(sourceHash);
        }

        public async Task AddSourceAsync(Source source)
        {
            var existing = await _context.Sources.FindAsync(source.SourceHash);
            if (existing == null)
            {
                _context.Sources.Add(source);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<CompiledContractSource>> GetSourcesForCompilationAsync(Guid compilationId)
        {
            return await _context.CompiledContractSources
                .Where(s => s.CompilationId == compilationId)
                .ToListAsync();
        }

        public async Task AddCompiledContractSourceAsync(CompiledContractSource source)
        {
            var existing = await _context.CompiledContractSources.FindAsync(source.Id);
            if (existing == null)
            {
                _context.CompiledContractSources.Add(source);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<SourcifyMatch> GetSourcifyMatchAsync(long verifiedContractId)
        {
            return await _context.SourcifyMatches
                .FirstOrDefaultAsync(m => m.VerifiedContractId == verifiedContractId);
        }

        public async Task AddSourcifyMatchAsync(SourcifyMatch match)
        {
            var existing = await _context.SourcifyMatches.FindAsync(match.Id);
            if (existing == null)
            {
                _context.SourcifyMatches.Add(match);
                await _context.SaveChangesAsync();
            }
        }

        public async Task BulkInsertAsync<T>(IEnumerable<T> entities) where T : class
        {
            _context.Set<T>().AddRange(entities);
            await _context.SaveChangesAsync();
        }
    }
}
