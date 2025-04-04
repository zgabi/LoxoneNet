﻿using System.Text.Json.Serialization;

namespace Certes.Jws
{
    /// <summary>
    /// Represents a JWK using Elliptic Curve.
    /// </summary>
    /// <seealso cref="Certes.Jws.JsonWebKey" />
    internal class EcJsonWebKey : JsonWebKey
    {
        /// <summary>
        /// Gets or sets the curve identifies the cryptographic curve used with the key.
        /// </summary>
        /// <value>
        /// The curve identifies the cryptographic curve used with the key.
        /// </value>
        [JsonPropertyName("crv")]
        [JsonPropertyOrder(1)]
        internal string Curve { get; set; }

        /// <summary>
        /// Gets or sets the x coordinate for the Elliptic Curve point.
        /// </summary>
        /// <value>
        /// The x coordinate for the Elliptic Curve point.
        /// </value>
        [JsonPropertyName("x")]
        [JsonPropertyOrder(3)]
        internal string X { get; set; }

        /// <summary>
        /// Gets or sets the y coordinate for the Elliptic Curve point.
        /// </summary>
        /// <value>
        /// The y coordinate for the Elliptic Curve point.
        /// </value>
        [JsonPropertyName("y")]
        [JsonPropertyOrder(4)]
        internal string Y { get; set; }
    }
}
