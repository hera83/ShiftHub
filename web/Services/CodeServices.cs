namespace ShiftHub.Services
{
    public class CodeServices
    {
        public string Base64Encode(string Value)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(Value);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public string Base64Decode(string Value)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(Value);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);

        }

    }

}