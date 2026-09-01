using System;
using System.ComponentModel.DataAnnotations;

namespace DynamicsAdapter.Web.Configuration
{
    /// <summary>
    /// Root configuration for connecting to Dynamics. <see cref="AuthenticationType"/> selects
    /// which of <see cref="ADFS"/> (on-premise) or <see cref="EntraId"/> (cloud) is used.
    /// </summary>
    public class DynamicsOptions
    {
        public const string OnPremise = "OnPremise";
        public const string Cloud = "Cloud";

        [Required]
        public string AuthenticationType { get; set; } = OnPremise;

        public AdfsOptions ADFS { get; set; }

        public EntraIdOptions EntraId { get; set; }

        public bool IsCloud => string.Equals(AuthenticationType, Cloud, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The base address of the Dynamics Web API (OData endpoint) for the currently selected
        /// authentication type.
        /// </summary>
        public string DynamicsApiEndpointUrl => IsCloud ? EntraId?.DynamicsApiEndpointUrl : ADFS?.DynamicsApiEndpointUrl;
    }

    /// <summary>
    /// On-premise ADFS resource-owner-password-credentials configuration (legacy, until cutoff).
    /// </summary>
    public class AdfsOptions
    {
        [Required]
        public string DynamicsApiEndpointUrl { get; set; }

        [Required]
        public string OAuth2TokenEndpoint { get; set; }

        [Required]
        public string ClientId { get; set; }

        [Required]
        public string ClientSecret { get; set; }

        [Required]
        public string ServiceAccountName { get; set; }

        [Required]
        public string ServiceAccountPassword { get; set; }

        [Required]
        public string ResourceName { get; set; }
    }

    /// <summary>
    /// Microsoft Entra ID (Azure AD) client-credentials configuration for the Dynamics Cloud instance.
    /// </summary>
    public class EntraIdOptions
    {
        [Required]
        public string DynamicsApiEndpointUrl { get; set; }

        [Required]
        public string TenantId { get; set; }

        [Required]
        public string ClientId { get; set; }

        [Required]
        public string ClientSecret { get; set; }

        [Required]
        public string ResourceName { get; set; }
    }
}
