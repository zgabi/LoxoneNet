﻿using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Certes.Acme.Resource
{
    /// <summary>
    /// Represents the status of <see cref="Authorization"/>.
    /// </summary>
    //[JsonConverter(typeof(StringEnumConverter))]
    public enum AuthorizationStatus
    {
        /// <summary>
        /// The pending status.
        /// </summary>
        [EnumMember(Value = "pending")]
        Pending,

        /// <summary>
        /// The valid status.
        /// </summary>
        [EnumMember(Value = "valid")]
        Valid,

        /// <summary>
        /// The invalid status.
        /// </summary>
        [EnumMember(Value = "invalid")]
        Invalid,

        /// <summary>
        /// The revoked status.
        /// </summary>
        [EnumMember(Value = "revoked")]
        Revoked,

        /// <summary>
        /// The deactivated status.
        /// </summary>
        [EnumMember(Value = "deactivated")]
        Deactivated,

        /// <summary>
        /// The expired status.
        /// </summary>
        [EnumMember(Value = "expired")]
        Expired,
    }
}
