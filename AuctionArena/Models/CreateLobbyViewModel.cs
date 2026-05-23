using System.ComponentModel.DataAnnotations;

namespace AuctionArena.Models
{
    public class CreateLobbyViewModel
    {
        [Required(ErrorMessage = "Host name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Host name must be between 2 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_.]+$", ErrorMessage = "Host name contains invalid characters")]
        public string HostName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Game name is required")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Game name must be between 2 and 50 characters")]
        [RegularExpression(@"^[a-zA-Z0-9\s\-_.]+$", ErrorMessage = "Game name contains invalid characters")]
        public string GameName { get; set; } = string.Empty;

        [StringLength(100, MinimumLength = 0, ErrorMessage = "Password must be at most 100 characters")]
        public string? Password { get; set; }

        [Required(ErrorMessage = "Number of teams is required")]
        [Range(2, 12, ErrorMessage = "Number of teams must be between 2 and 12")]
        public int TotalTeams { get; set; }

        [Required(ErrorMessage = "Players per team is required")]
        [Range(1, 50, ErrorMessage = "Players per team must be between 1 and 50")]
        public int PlayersPerTeam { get; set; }

        [Required(ErrorMessage = "Points per team is required")]
        [Range(100, 100000, ErrorMessage = "Points per team must be between 100 and 100,000")]
        public int PointsPerTeam { get; set; }

        [Required(ErrorMessage = "Min players per team is required")]
        [Range(1, 50, ErrorMessage = "Min players per team must be between 1 and 50")]
        public int MinPlayersPerTeam { get; set; }

        [Required(ErrorMessage = "Max players per team is required")]
        [Range(1, 50, ErrorMessage = "Max players per team must be between 1 and 50")]
        public int MaxPlayersPerTeam { get; set; }

        [ValidateTeamsAttribute]
        public List<TeamSetup> Teams { get; set; } = new();
    }

    public class ValidateTeamsAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var model = (CreateLobbyViewModel)validationContext.ObjectInstance;
            var teams = value as List<TeamSetup>;

            if (teams == null || teams.Count != model.TotalTeams)
                return new ValidationResult($"Please set up exactly {model.TotalTeams} teams");

            foreach (var team in teams)
            {
                if (string.IsNullOrWhiteSpace(team.TeamName))
                    return new ValidationResult("All team names are required");
                if (string.IsNullOrWhiteSpace(team.OwnerName))
                    return new ValidationResult("All owner names are required");
            }

            var ownerNames = teams.Select(t => t.OwnerName.Trim().ToLowerInvariant()).ToList();
            if (ownerNames.Distinct().Count() != ownerNames.Count)
                return new ValidationResult("Owner names must be unique");

            if (model.MinPlayersPerTeam > model.MaxPlayersPerTeam)
                return new ValidationResult("Min players cannot exceed max players");

            return ValidationResult.Success;
        }
    }
}
