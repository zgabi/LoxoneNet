﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Certes.Properties {
    using System;
    using System.Reflection;


    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Strings {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Strings() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("LoxoneNet.Certes.Properties.Strings", typeof(Strings).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fail to fetch new nonce..
        /// </summary>
        internal static string ErrorFetchNonce {
            get {
                return ResourceManager.GetString("ErrorFetchNonce", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fail to load resource from &apos;{0}&apos;..
        /// </summary>
        internal static string ErrorFetchResource {
            get {
                return ResourceManager.GetString("ErrorFetchResource", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Fail to finalize order..
        /// </summary>
        internal static string ErrorFinalizeFailed {
            get {
                return ResourceManager.GetString("ErrorFinalizeFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Illegal base64url string..
        /// </summary>
        internal static string ErrorInvalidBase64String {
            get {
                return ResourceManager.GetString("ErrorInvalidBase64String", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invaid key data..
        /// </summary>
        internal static string ErrorInvalidKeyData {
            get {
                return ResourceManager.GetString("ErrorInvalidKeyData", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Can not finalize order with status &apos;{0}&apos;..
        /// </summary>
        internal static string ErrorInvalidOrderStatusForFinalize {
            get {
                return ResourceManager.GetString("ErrorInvalidOrderStatusForFinalize", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Can not find issuer &apos;{0}&apos; for certificate &apos;{1}&apos;..
        /// </summary>
        internal static string ErrorIssuerNotFound {
            get {
                return ResourceManager.GetString("ErrorIssuerNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Certificate data missing, please fetch the certificate from &apos;{0}&apos;..
        /// </summary>
        internal static string ErrorMissingCertificateData {
            get {
                return ResourceManager.GetString("ErrorMissingCertificateData", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unsupported resource type &apos;{0}&apos;..
        /// </summary>
        internal static string ErrorUnsupportedResourceType {
            get {
                return ResourceManager.GetString("ErrorUnsupportedResourceType", resourceCulture);
            }
        }
    }
}
