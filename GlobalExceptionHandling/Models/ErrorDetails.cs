using System.Text.Json;

namespace GlobalExceptionHandling.Models
{
    public class ErrorDetails //we are going to use this class for details of our message
    {
        public int StatusCode { get; set; }
        public string? Message { get; set; }

        public override string ToString()//GlobalExceptionHandling yapısında hata bilgilerini JSON formatına çevirmek için.
        {
            return JsonSerializer.Serialize(this);//this,içinde bulunduğun sınıftan oluşturulan nesneyi ifade eder.
        }
    }
}
