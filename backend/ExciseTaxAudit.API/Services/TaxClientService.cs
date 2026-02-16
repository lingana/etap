using ExciseTaxAudit.API.Models;
using ExciseTaxAudit.API.DTOs;
using ExciseTaxAudit.API.Data;
using Microsoft.EntityFrameworkCore;

namespace ExciseTaxAudit.API.Services
{
    public interface ITaxClientService
    {
        Task<List<TaxTypeDto>> GetAllTaxTypesAsync();
        Task<List<ClientDto>> GetClientsByTaxTypeAsync(int taxTypeId);
        Task<List<EngagementDto>> GetEngagementsByClientAsync(int clientId);
        Task<EngagementDto?> GetEngagementByIdAsync(int engagementId);
        Task<ClientDto> CreateClientAsync(CreateClientRequest request);
        Task<EngagementDto> CreateEngagementAsync(CreateEngagementRequest request);
    }

    public class TaxClientService : ITaxClientService
    {
        private readonly AuditContext _context;

        public TaxClientService(AuditContext context)
        {
            _context = context;
        }

        public async Task<List<TaxTypeDto>> GetAllTaxTypesAsync()
        {
            return await _context.TaxTypes
                .Where(t => t.IsActive)
                .Select(t => new TaxTypeDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    Description = t.Description,
                    Icon = t.Icon,
                    TaxRate = t.TaxRate,
                    IsActive = t.IsActive
                })
                .ToListAsync();
        }

        public async Task<List<ClientDto>> GetClientsByTaxTypeAsync(int taxTypeId)
        {
            return await _context.Clients
                .Where(c => c.TaxTypeId == taxTypeId && c.IsActive)
                .Select(c => new ClientDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    EIN = c.EIN,
                    Industry = c.Industry,
                    ContactPerson = c.ContactPerson,
                    ContactEmail = c.ContactEmail,
                    ContactPhone = c.ContactPhone,
                    Address = c.Address,
                    City = c.City,
                    State = c.State,
                    TaxTypeId = c.TaxTypeId,
                    IsActive = c.IsActive
                })
                .ToListAsync();
        }

        public async Task<List<EngagementDto>> GetEngagementsByClientAsync(int clientId)
        {
            return await _context.Engagements
                .Where(e => e.ClientId == clientId && e.IsActive)
                .OrderByDescending(e => e.FiscalYear)
                .Select(e => new EngagementDto
                {
                    Id = e.Id,
                    ClientId = e.ClientId,
                    ExternalEngagementId = e.ExternalEngagementId,
                    EngagementName = e.EngagementName,
                    FiscalYear = e.FiscalYear,
                    FiscalYearStart = e.FiscalYearStart,
                    FiscalYearEnd = e.FiscalYearEnd,
                    EngagementType = e.EngagementType.ToString(),
                    Status = e.Status.ToString(),
                    Description = e.Description,
                    EstimatedPotentialRecovery = e.EstimatedPotentialRecovery,
                    ActualRecovery = e.ActualRecovery,
                    IsActive = e.IsActive
                })
                .ToListAsync();
        }

        public async Task<EngagementDto?> GetEngagementByIdAsync(int engagementId)
        {
            var engagement = await _context.Engagements
                .FirstOrDefaultAsync(e => e.Id == engagementId);

            if (engagement == null)
                return null;

            return new EngagementDto
            {
                Id = engagement.Id,
                ClientId = engagement.ClientId,
                ExternalEngagementId = engagement.ExternalEngagementId,
                EngagementName = engagement.EngagementName,
                FiscalYear = engagement.FiscalYear,
                FiscalYearStart = engagement.FiscalYearStart,
                FiscalYearEnd = engagement.FiscalYearEnd,
                EngagementType = engagement.EngagementType.ToString(),
                Status = engagement.Status.ToString(),
                Description = engagement.Description,
                EstimatedPotentialRecovery = engagement.EstimatedPotentialRecovery,
                ActualRecovery = engagement.ActualRecovery,
                IsActive = engagement.IsActive
            };
        }

        public async Task<ClientDto> CreateClientAsync(CreateClientRequest request)
        {
            var client = new Client
            {
                Name = request.Name,
                EIN = request.EIN,
                Industry = request.Industry,
                ContactPerson = request.ContactPerson,
                ContactEmail = request.ContactEmail,
                ContactPhone = request.ContactPhone,
                Address = request.Address,
                City = request.City,
                State = request.State,
                TaxTypeId = request.TaxTypeId,
                IsActive = true
            };

            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            return new ClientDto
            {
                Id = client.Id,
                Name = client.Name,
                EIN = client.EIN,
                Industry = client.Industry,
                ContactPerson = client.ContactPerson,
                ContactEmail = client.ContactEmail,
                ContactPhone = client.ContactPhone,
                Address = client.Address,
                City = client.City,
                State = client.State,
                TaxTypeId = client.TaxTypeId,
                IsActive = client.IsActive
            };
        }

        public async Task<EngagementDto> CreateEngagementAsync(CreateEngagementRequest request)
        {
            var engagement = new Engagement
            {
                ClientId = request.ClientId,
                ExternalEngagementId = request.ExternalEngagementId,
                EngagementName = request.EngagementName,
                FiscalYear = request.FiscalYear,
                FiscalYearStart = request.FiscalYearStart,
                FiscalYearEnd = request.FiscalYearEnd,
                EngagementType = Enum.Parse<EngagementTypeEnum>(request.EngagementType),
                Status = EngagementStatusEnum.Planning,
                Description = request.Description,
                IsActive = true
            };

            _context.Engagements.Add(engagement);
            await _context.SaveChangesAsync();

            return new EngagementDto
            {
                Id = engagement.Id,
                ClientId = engagement.ClientId,
                ExternalEngagementId = engagement.ExternalEngagementId,
                EngagementName = engagement.EngagementName,
                FiscalYear = engagement.FiscalYear,
                FiscalYearStart = engagement.FiscalYearStart,
                FiscalYearEnd = engagement.FiscalYearEnd,
                EngagementType = engagement.EngagementType.ToString(),
                Status = engagement.Status.ToString(),
                Description = engagement.Description,
                EstimatedPotentialRecovery = engagement.EstimatedPotentialRecovery,
                ActualRecovery = engagement.ActualRecovery,
                IsActive = engagement.IsActive
            };
        }
    }
}
