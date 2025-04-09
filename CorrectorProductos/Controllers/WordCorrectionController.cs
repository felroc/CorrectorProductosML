using Microsoft.AspNetCore.Mvc;

namespace CorrectorProductos.Controllers
{
    public class WordCorrectionController : Controller
    {
        //public IActionResult Index()
        //{
        //    return View();
        //}

        private readonly WordCorrectionService _wordCorrectionService;

        public WordCorrectionController(WordCorrectionService wordCorrectionService)
        {
            _wordCorrectionService = wordCorrectionService;
        }


        [HttpGet]
        [Route("api/correct/{misspelledWord}")]
        public IActionResult CorrectWord(string misspelledWord)
        {
            if (string.IsNullOrEmpty(misspelledWord))
            {
                return BadRequest("Please provide a misspelled word to be corrected.");
            }

            var correctedWord = _wordCorrectionService.Predict(misspelledWord);
            return Ok(correctedWord);
        }



        [HttpPost("predict")]
        public IActionResult Predict([FromBody] WordCorrectionRequest request)
        {
            if (string.IsNullOrEmpty(request.MisspelledWord))
                return BadRequest("La palabra mal escrita es requerida.");

            var correctedWord = _wordCorrectionService.Predict(request.MisspelledWord);
            return Ok(new { CorrectedWord = correctedWord });
        }
    }


    public class WordCorrectionRequest
    {
        public string MisspelledWord { get; set; }
    }
}
