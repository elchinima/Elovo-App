namespace Elovo.Web.Models
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public int StatusCode { get; set; }

        public string Title => StatusCode switch
        {
            400 => "Bad request",
            401 => "Sign in required",
            403 => "Access denied",
            404 => "Page not found",
            500 => "Server error",
            _ => "Something went wrong"
        };

        public string Message => StatusCode switch
        {
            400 => "The request could not be processed.",
            401 => "Please sign in to continue.",
            403 => "You do not have access to this page.",
            404 => "The page you are looking for does not exist or was moved.",
            500 => "The server hit an unexpected problem.",
            _ => "We could not complete this request."
        };

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
