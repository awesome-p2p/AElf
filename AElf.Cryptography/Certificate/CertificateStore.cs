﻿using System;
using System.Collections.Generic;
using System.IO;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.X509;

namespace AElf.Cryptography.Certificate
{
    public class CertificateStore
    {
        private string _dataDirectory;
        private const string FolderName = "certs";
        private const string CertExtension = ".cert.pem";
        private const string KeyExtension = ".key.pem";


        public CertificateStore(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
        }

        public RSAKeyPair WriteKeyAndCertificate(string name, string ipAddress)
        {
            // generate key pair
            var keyPair = new RSAKeyPairGenerator().Generate();
            var certGenerator = GetCertificateGenerator(keyPair);
            
            // Todo: "127.0.0.1" would be removed eventually
            certGenerator.AddALternativeName(ipAddress);
            
            // generate certificate
            var cert = certGenerator.Generate(keyPair.PrivateKey);
            var path = Path.Combine(_dataDirectory, FolderName);
            WriteKeyAndCertificate(cert, path, name, CertExtension);
            WriteKeyAndCertificate(keyPair.PrivateKey, path, name, KeyExtension);
            return keyPair;
        }

        public bool AddCertificate(string name, string certificate)
        {
            Directory.CreateDirectory(Path.Combine(_dataDirectory, FolderName));
            using (StreamWriter streamWriter = new StreamWriter(Path.Combine(_dataDirectory, FolderName, name + CertExtension)))
            {
                PemWriter pem = new PemWriter(streamWriter);
                try
                {
                    pem.Writer.WriteAsync(certificate);
                    return true;
                }
                finally
                {
                    pem.Writer.Close();
                }
            }
        }

        private CertGenerator GetCertificateGenerator(RSAKeyPair keyPair)
        {
            return  new CertGenerator().SetPublicKey(keyPair.PublicKey);
        }

        public bool WriteKeyAndCertificate(object obj, string dir, string fileName, string extension)
        {
            // create directory if not exists
            Directory.CreateDirectory(dir);
            
            using (StreamWriter outputFile = new StreamWriter(Path.Combine(dir, fileName + extension), true)) {
                PemWriter pw = new PemWriter(outputFile);
                try
                {
                    switch (obj)
                    {
                        case X509Certificate cert:
                            pw.WriteObject(cert);
                            break;
                        case AsymmetricKeyParameter key:
                            pw.WriteObject(key);
                            break;
                    }

                    return true;
                }
                finally
                {
                    pw.Writer.Close();
                }
            }
        }

        public string GetCertificate(string name)
        {
            try
            {
                string crt = File.ReadAllText(Path.Combine(_dataDirectory, FolderName, name + CertExtension));
                return crt;
            }
            catch (Exception e)
            {
                return null;
            }
        }
        
        public string GetPrivateKey(string name)
        {
            try
            {
                string crt = File.ReadAllText(Path.Combine(_dataDirectory, FolderName, name + KeyExtension));
                return crt;
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}