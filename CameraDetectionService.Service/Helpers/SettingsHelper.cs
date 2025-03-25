using CameraDetectionService.Service.Models;
using System.Security.Cryptography;
using System.Text;

namespace CameraDetectionService.Service.Helpers;

public class SettingsHelper
{
  private static readonly byte[] Key = Encoding.UTF8.GetBytes("your-encryption-key-here"); // 32 bytes for AES-256
  private static readonly byte[] IV = Encoding.UTF8.GetBytes("your-iv-key-here"); // 16 bytes for AES

  public static void SaveConfig(Config config)
  {
    var json = System.Text.Json.JsonSerializer.Serialize(config);
    var encryptedJson = EncryptString(json);
    File.WriteAllText("config.json", encryptedJson);
  }

  public static Config LoadConfig()
  {
    try
    {
      if (!File.Exists("config.json"))
        return new Config();

      var encryptedJson = File.ReadAllText("config.json");
      if (!isencrypted(encryptedJson))
        return System.Text.Json.JsonSerializer.Deserialize<Config>(encryptedJson)!;

      var json = DecryptString(encryptedJson);
      return System.Text.Json.JsonSerializer.Deserialize<Config>(json)!;
    }
    catch (Exception)
    {
      return new Config();
    }
  }
  private static bool isencrypted(string json)
  {
    try
    {
      _ = System.Text.Json.JsonSerializer.Deserialize<Config>(json);
      return false;
    }
    catch (Exception)
    {
      return true;
    }
  }

  private static string EncryptString(string plainText)
  {
    using (Aes aesAlg = Aes.Create())
    {
      aesAlg.Key = Key;
      aesAlg.IV = IV;

      ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

      using (MemoryStream msEncrypt = new MemoryStream())
      {
        using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
        {
          swEncrypt.Write(plainText);
        }
        return Convert.ToBase64String(msEncrypt.ToArray());
      }
    }
  }

  private static string DecryptString(string cipherText)
  {
    using (Aes aesAlg = Aes.Create())
    {
      aesAlg.Key = Key;
      aesAlg.IV = IV;

      ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

      using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
      using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
      using (StreamReader srDecrypt = new StreamReader(csDecrypt))
      {
        return srDecrypt.ReadToEnd();
      }
    }
  }
}