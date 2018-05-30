﻿//-----------------------------------------------------------------------------
// FILE:	    VaultClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Net;
using Neon.Retry;

namespace Neon.Cluster
{
    /// <summary>
    /// A light-weight (and incomplete) HashiCorp Vault client.
    /// </summary>
    public class VaultClient : IDisposable
    {
        private const string vaultApiVersion = "v1";

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Opens a Vault connection with an optional Vault token.
        /// </summary>
        /// <param name="uri">The Vault server URI.</param>
        /// <param name="token">The optional token.</param>
        /// <returns>The <see cref="VaultClient"/>.</returns>
        /// <remarks>
        /// <note>
        /// You may pass <paramref name="token"/> as <c>null</c> if you only need to
        /// make requests to insecure endpoints.
        /// </note>
        /// </remarks>
        public static VaultClient OpenWithToken(Uri uri, string token = null)
        {
            Covenant.Requires<ArgumentNullException>(uri != null);

            var vaultClient = new VaultClient(uri);

            if (!string.IsNullOrEmpty(token))
            {
                vaultClient.jsonClient.HttpClient.DefaultRequestHeaders.Add("X-Vault-Token", token);
            }

            return vaultClient;
        }

        /// <summary>
        /// Opens a Vault connection using Vault AppRole credentials.
        /// </summary>
        /// <param name="uri">The Vault server URI.</param>
        /// <param name="roleId">The role ID.</param>
        /// <param name="secretId">The secret ID.</param>
        /// <returns>The <see cref="VaultClient"/>.</returns>
        public static VaultClient OpenWithAppRole(Uri uri, string roleId, string secretId)
        {
            Covenant.Requires<ArgumentNullException>(uri != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(roleId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secretId));

            var vaultClient = new VaultClient(uri);

            dynamic loginPayload = new ExpandoObject();

            loginPayload.role_id   = roleId;
            loginPayload.secret_id = secretId;

            var loginResponse = vaultClient.jsonClient.PostAsync($"/{vaultApiVersion}/auth/approle/login", loginPayload).Result.AsDynamic();

            vaultClient.jsonClient.HttpClient.DefaultRequestHeaders.Add("X-Vault-Token", (string)loginResponse.auth.client_token);

            return vaultClient;
        }

        //---------------------------------------------------------------------
        // Instance members

        private object          syncLock = new object();
        private JsonClient      jsonClient;

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="uri">The Vault server URI.</param>
        private VaultClient(Uri uri)
        {
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression                    = DecompressionMethods.Deflate | DecompressionMethods.GZip,
                ServerCertificateCustomValidationCallback = ValidateRemoteCertificate
            };

            jsonClient                 = new JsonClient(handler, disposeHandler: true);
            jsonClient.SafeRetryPolicy = new ExponentialRetryPolicy(TransientDetector.NetworkOrHttp);
            jsonClient.BaseAddress     = uri;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~VaultClient()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (syncLock)
            {
                if (jsonClient != null)
                {
                    jsonClient.Dispose();
                    jsonClient = null;
                }
            }
        }

        /// <summary>
        /// Returns the underlying <see cref="Neon.Net.JsonClient"/>.
        /// </summary>
        public JsonClient JsonClient
        {
            get { return jsonClient; }
        }

        /// <summary>
        /// Indicates the self-signed server certificates are to be trusted.
        /// This defaults to <c>true</c>.
        /// </summary>
        public bool AllowSelfSignedCertificates { get; set; } = true;
        
        /// <summary>
        /// Verifies the remote Secure Sockets Layer (SSL) certificate used for authentication.
        /// </summary>
        /// <param name="sender">An object that contains state information for this validation.</param>
        /// <param name="certificate">The certificate used to authenticate the remote party.</param>
        /// <param name="chain">The chain of certificate authorities associated with the remote certificate.</param>
        /// <param name="sslPolicyErrors">One or more errors associated with the remote certificate.</param>
        /// <returns><c>true</c> if the certificate is to be accepted.</returns>
        private  bool ValidateRemoteCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (AllowSelfSignedCertificates)
            {
                return (sslPolicyErrors & ~(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch)) == 0;
            }
            else
            {
                return sslPolicyErrors == SslPolicyErrors.None;
            }
        }

        /// <summary>
        /// Removes any leading forward slash (/) from a path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>The normalized path.</returns>
        private static string Normalize(string path)
        {
            if (path.StartsWith("/"))
            {
                return path.Substring(1);
            }
            else
            {
                return path;
            }
        }

        /// <summary>
        /// Returns the current Vault instance health status.
        /// </summary>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="VaultStatus"/>.</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<VaultStatus> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            // We need to make an unsafe call because Vault will return [503-Service Unavailable] when not 
            // initialized or sealed.

            var requestUri   = $"/{vaultApiVersion}/sys/health";
            var jsonResponse = await jsonClient.GetUnsafeAsync(requestUri, cancellationToken: cancellationToken);

            try
            {
                var response = jsonResponse.AsDynamic();
            
                return new VaultStatus()
                {
                    IsInitialized = (bool)response.GetValue("initialized"),
                    IsSealed      = (bool)response.GetValue("sealed"),
                    IsStandby     = (bool)response.GetValue("standby"),
                    Version       = (string)response.GetValue("version")
                };
            }
            catch
            {
                throw new HttpException(jsonResponse.StatusCode, "Unable to parse the status response.", requestUri);
            }
        }

        /// <summary>
        /// Unseals the Vault instance.
        /// </summary>
        /// <param name="credentials">The Vault credentials.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task UnsealAsync(VaultCredentials credentials, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(credentials != null);

            await jsonClient.PutAsync($"/{vaultApiVersion}/sys/unseal", new JObject(new JProperty("reset", true)), cancellationToken: cancellationToken);

            for (int i = 0; i < credentials.KeyThreshold; i++)
            {
                await jsonClient.PutAsync($"/{vaultApiVersion}/sys/unseal", new JObject(new JProperty("key", credentials.UnsealKeys[i])), cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Determines whether a Vault object exists.
        /// </summary>
        /// <param name="path">The object path.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns><c>true</c> if the object exists.</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            try
            {
                await ReadDynamicAsync(path, cancellationToken: cancellationToken);

                return true;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Reads the Vault object located at the specified path as a dynamic throwing
        /// a <see cref="KeyNotFoundException"/> if the path doesn't exist.
        /// </summary>
        /// <param name="path">The object path.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The result as a <c>dynamic</c> object.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if no object is present at <paramref name="path"/>.
        /// </exception>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<dynamic> ReadDynamicAsync(string path, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            try
            {
                return (await jsonClient.GetAsync($"/{vaultApiVersion}/{Normalize(path)}", null, cancellationToken))
                    .AsDynamic()
                    .data;
            }
            catch (HttpException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new KeyNotFoundException($"Vault [path={path}] not found.", e);
                }

                throw new HttpException($"Unable to read Vault bytes from [path={path}]", e);
            }
        }

        /// <summary>
        /// Reads the Vault object located at the specified path as a dynamic, returning
        /// <c>null</c> if the path does not exist.
        /// </summary>
        /// <param name="path">The object path.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The result as a <c>dynamic</c> object or <c>null</c>.</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<dynamic> ReadDynamicOrDefaultAsync(string path, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            try
            {
                return (await jsonClient.GetAsync($"/{vaultApiVersion}/{Normalize(path)}", null, cancellationToken))
                    .AsDynamic()
                    .data;
            }
            catch (HttpException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    return default(dynamic);
                }

                throw new HttpException($"Unable to read Vault bytes from [path={path}]", e);
            }
        }

        /// <summary>
        /// Reads and deserializes the Vault object located at the specified path as JSON
        /// throwing a <see cref="KeyNotFoundException"/> if the path doesn't exist.
        /// </summary>
        /// <typeparam name="T">The type being read.</typeparam>
        /// <param name="path">The object path.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The result as a <c>dynamic</c> object.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if no object is present at <paramref name="path"/>.
        /// </exception>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            try
            {
                var jsonText = (await jsonClient.GetAsync($"/{vaultApiVersion}/{Normalize(path)}", null, cancellationToken))
                    .AsDynamic()
                    .data
                    .ToString();

                return NeonHelper.JsonDeserialize<T>(jsonText);
            }
            catch (HttpException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new KeyNotFoundException($"Vault [path={path}] not found.", e);
                }

                throw new HttpException($"Unable to read Vault bytes from [path={path}]", e);
            }
        }

        /// <summary>
        /// Reads and deserializes the Vault object located at the specified path as JSON,
        /// returning the default value if the path doesn't exist.
        /// </summary>
        /// <typeparam name="T">The type being read.</typeparam>
        /// <param name="path">The object path.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The result as a <c>dynamic</c> object or <c>null</c> if the path doesn't exist.</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<T> ReadJsonOrDefaultAsync<T>(string path, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            try
            {
                var jsonText = (await jsonClient.GetAsync($"/{vaultApiVersion}/{Normalize(path)}", null, cancellationToken))
                    .AsDynamic()
                    .data
                    .ToString();

                return NeonHelper.JsonDeserialize<T>(jsonText);
            }
            catch (HttpException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    return default(T);
                }

                throw new HttpException($"[status={e.StatusCode}]: Unable to read Vault bytes from [path={path}]", e);
            }
        }

        /// <summary>
        /// Writes a <c>dynamic</c> value as JSON to a Vault path.
        /// </summary>
        /// <param name="path">The object path.</param>
        /// <param name="value">The pbject value to be written or <c>null</c>.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The result as a <c>dynamic</c> object.</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<dynamic> WriteJsonAsync(string path, object value, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));
            Covenant.Requires<ArgumentNullException>(value != null);

            return (await jsonClient.PostAsync($"/{vaultApiVersion}/{Normalize(path)}", value, null, cancellationToken))
                .AsDynamic();
        }

        /// <summary>
        /// Writes a base-64 encoded byte array to a Vault path.
        /// </summary>
        /// <param name="path">The object path.</param>
        /// <param name="bytes">The value to be written.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The result as a <c>dynamic</c> object.</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<dynamic> WriteBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));
            Covenant.Requires<ArgumentNullException>(bytes != null);

            var bytesObject = new JObject();

            bytesObject.Add("value", Convert.ToBase64String(bytes).Trim());

            return await WriteJsonAsync(path, bytesObject, cancellationToken);
        }

        /// <summary>
        /// Writes a string to a Vault path.  Note that the string will be UTF-8 encoded
        /// and then persisted as a base-64 encoded byte array.
        /// </summary>
        /// <param name="path">The object path.</param>
        /// <param name="value">The value to be written.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The result as a <c>dynamic</c> object.</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<dynamic> WriteStringAsync(string path, string value, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));
            Covenant.Requires<ArgumentNullException>(value != null);

            return await WriteBytesAsync(path, Encoding.UTF8.GetBytes(value), cancellationToken);
        }

        /// <summary>
        /// Reads the Vault object located at the specified path as a decoded base-64 byte array,
        /// throwing a <see cref="KeyNotFoundException"/> if the path doesn't exist.
        /// </summary>
        /// <param name="path">The object path.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The byte array.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if no object is present at <paramref name="path"/>.
        /// </exception>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<byte[]> ReadBytesAsync(string path, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            try
            {
                var bytesObject = (await jsonClient.GetAsync($"/{vaultApiVersion}/{Normalize(path)}", null, cancellationToken))
                    .AsDynamic()
                    .data;

                return Convert.FromBase64String((string)bytesObject.value);
            }
            catch (HttpException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new KeyNotFoundException($"Vault [path={path}] not found.", e);
                }

                throw new HttpException($"Unable to read Vault bytes from [path={path}]", e);
            }
        }

        /// <summary>
        /// Reads the Vault object located at the specified path as a decoded base-64 byte array,
        /// returning <c>null</c> if the path doesn't exist.
        /// </summary>
        /// <param name="path">The object path.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The byte array or <c>null</c> if the path doesn't exist.</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<byte[]> ReadBytesOrDefaultAsync(string path, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            try
            {
                var bytesObject = (await jsonClient.GetAsync($"/{vaultApiVersion}/{Normalize(path)}", null, cancellationToken))
                    .AsDynamic()
                    .data;

                return Convert.FromBase64String((string)bytesObject.value);
            }
            catch (HttpException e)
            {
                if (e.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }

                throw new HttpException($"Unable to read Vault bytes from [path={path}]", e);
            }
        }

        /// <summary>
        /// Reads the Vault string located at the specified path by decoding a base-64 byte array
        /// and then decoding the bytes as UTF-8, throwing a <see cref="KeyNotFoundException"/>
        /// if the path doesn't exist.
        /// </summary>
        /// <param name="path">The object path.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The string.</returns>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if no object is present at <paramref name="path"/>.
        /// </exception>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<string> ReadStringAsync(string path, CancellationToken cancellationToken = default)
        {
            var bytes = await ReadBytesAsync(path, cancellationToken);

            if (bytes == null)
            {
                return null;
            }

            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Reads the Vault string located at the specified path by decoding a base-64 byte array
        /// and then decoding the bytes as UTF-8, returning <c>null</c> if the path doesn't exist.
        /// </summary>
        /// <param name="path">The object path.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The string or <c>null</c> if the path doesn't exist..</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<string> ReadStringOrDefaultAsync(string path, CancellationToken cancellationToken = default)
        {
            var bytes = await ReadBytesOrDefaultAsync(path, cancellationToken);

            if (bytes == null)
            {
                return null;
            }

            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Deletes the value at a Vault path.
        /// </summary>
        /// <param name="path">The object path.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            var response = await JsonClient.DeleteUnsafeAsync($"/{vaultApiVersion}/{Normalize(path)}", null, cancellationToken);

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                case HttpStatusCode.NotFound:

                    return;

                default:

                    response.EnsureSuccess();
                    break;
            }
        }

        /// <summary>
        /// Lists the keys beneath a Vault path.
        /// </summary>
        /// <param name="path">The vault path, with or without a trailing forward slash (<b>/</b>).</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>A string list.</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<IEnumerable<string>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            if (!path.EndsWith("/"))
            {
                path += "/";
            }

            var result = await jsonClient.GetUnsafeAsync($"/{vaultApiVersion}/{Normalize(path)}", new ArgDictionary() { { "list", "true" } }, cancellationToken);

            if (result.StatusCode == HttpStatusCode.NotFound)
            {
                return new string[0];
            }
            else
            {
                result.EnsureSuccess();
            }

            dynamic v = result.AsDynamic();

            return (string[])((JArray)v.data.keys).ToObject(typeof(string[]));
        }

        /// <summary>
        /// Returns credentials for a Vault AppRole.
        /// </summary>
        /// <param name="roleName">The role name.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <returns>The <see cref="ClusterCredentials"/>.</returns>
        /// <exception cref="HttpException">Thrown for Vault communication problems.</exception>
        public async Task<ClusterCredentials> GetAppRoleCredentialsAsync(string roleName, CancellationToken cancellationToken = default)
        {
            Covenant.Requires<ArgumentException>(ClusterDefinition.IsValidName(roleName));

            string roleId;
            string secretId;

            // Verify that the role exists.

            try
            {
                await ReadDynamicAsync($"auth/approle/role/{roleName}", cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {
                throw new HttpException($"Unable to access Vault [AppRole={roleName}]", e);
            }

            // Fetch the role ID.

            try
            {
                var response = await ReadDynamicAsync($"auth/approle/role/{roleName}/role-id", cancellationToken: cancellationToken);

                roleId = response.role_id;
            }
            catch (Exception e)
            {
                throw new HttpException($"Unable to fetch the role ID for Vault [AppRole={roleName}]", e);
            }

            // Fetch a secret ID.

            try
            {
                var response = (await WriteJsonAsync($"auth/approle/role/{roleName}/secret-id", cancellationToken)).data;

                secretId = response.secret_id;
            }
            catch (Exception e)
            {
                throw new HttpException($"Unable to fetch the role ID for Vault [AppRole={roleName}]", e);
            }

            // Return the credentials.

            return ClusterCredentials.FromVaultRole(roleId, secretId);
        }
    }
}
