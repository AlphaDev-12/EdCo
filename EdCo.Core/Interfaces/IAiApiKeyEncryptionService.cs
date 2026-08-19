namespace EdCo.Core.Interfaces
{
    public interface IAiApiKeyEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }
}
