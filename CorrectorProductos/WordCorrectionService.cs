using Microsoft.AspNetCore.Mvc;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;


using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Text.Json;
using System.IO;

namespace CorrectorProductos
{

    public class WordCorrection
    {
        [LoadColumn(0)]
        public string MisspelledWord { get; set; }

        [LoadColumn(1)]
        public string CorrectWord { get; set; }
    }

    public class CorrectWordPrediction
    {
        [ColumnName("PredictedLabel")]
        public string PredictedWord { get; set; }
    }

    public class WordCorrectionService
    {
        private readonly MLContext _mlContext;
        private readonly PredictionEngine<WordCorrection, CorrectWordPrediction> _predictor;

        private readonly string stringConnection1;
        private string stringConnection2;

        public WordCorrectionService(string modelPath, string ExcelPath, string stringConnection1, string stringConnection2)
        {
            _mlContext = new MLContext();
            this.stringConnection1 = stringConnection1;
            this.stringConnection2 = stringConnection2;

            // Crear ML Context            
            var context = _mlContext.Transforms.Conversion
                .MapValueToKey("Label", nameof(WordCorrection.CorrectWord))
                .Append(_mlContext.Transforms.Text.FeaturizeText("Features", nameof(WordCorrection.MisspelledWord)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // entrenar desde la base de datos, desde un excel o cargar un modelo .zip

            bool zip = true;
            bool excel = false;

            /////////////////////////////////////////////////////////////////////////////////

            bool entrernar = true;
            if (entrernar)
            {
                // Configurar el Data Base Loader
                var loader = _mlContext.Data.CreateDatabaseLoader<WordCorrection>();

                // Connection String
                //var connectionString = "Server=192.200.9.131;Database=DB_GENESIS_CENTRAL; User Id=sa; Password=bofasa1$; Encrypt=True; TrustServerCertificate=True;";

                // EL ORDEN AFECTA VER EL ORDEN EN LA CLASE WordCorrection
                var sqlCommand = "SELECT MisspelledWord, CorrectWord " +
                    " FROM DB_GENESIS_CENTRAL.dbo.Productos_ML1 " + // <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<
                    " WHERE ISNULL(Estado,'A') = 'A' " +
                    " ; "; // -- AND MODIF IS NULL 

                // Script para obtener las palabras a partir de los productos
                /*var sqlCommand = "SELECT " +
					"DISTINCT " +
					"value AS 'CorrectWord', " +
					"SUBSTRING( value , 1, LEN(value) -1 ) as 'MisspelledWord' " +
					"FROM PRODUCCION.TBL_PRODUCTOS " +
					"CROSS APPLY STRING_SPLIT(ITEMNAME, ' ') " +
					"where U_LABORATORIO < 9999 " +
					"and ISNUMERIC(value) <> 1 " +
					"and value NOT LIKE '%[^a-zA-Z ]%' " +
					"AND LEN(value) > 3 " 
					;
				*/

                // Source
                DatabaseSource dbSource = new DatabaseSource(SqlClientFactory.Instance, stringConnection1, sqlCommand);

                // Cargar Datos desde SQL Server
                var dataView = loader.Load(dbSource);


                // Entrenar el modelo
                Console.WriteLine("Entrenando el modelo...");
                var model = context.Fit(dataView);

                // Save the trained model
                _mlContext.Model.Save(model, dataView.Schema, modelPath);

                Console.WriteLine($"Modelo guardado en: {modelPath}");

                // INIT Prediction Engine 
                _predictor = _mlContext.Model.CreatePredictionEngine<WordCorrection, CorrectWordPrediction>(model);
            }

            // ----------------------------------------------------------------------------------------------------------------------

            if (zip)
            {
                if (!File.Exists(modelPath))
                {
                    Console.WriteLine($"Modelo no encontrado en la ruta: {modelPath}");
                    throw new FileNotFoundException($"Modelo no encontrado en la ruta: {modelPath}");
                }

                // Cargar el modelo
                var model = _mlContext.Model.Load(modelPath, out _);

                if (model != null)
                {
                    // INIT Prediction Engine 
                    _predictor = _mlContext.Model.CreatePredictionEngine<WordCorrection, CorrectWordPrediction>(model);
                }
            }

            // ----------------------------------------------------------------------------------------------------------------------

            if (excel)
            {
                if (!File.Exists(ExcelPath))
                    throw new FileNotFoundException($"Modelo no encontrado en la ruta: {ExcelPath}");

                // Cargar los datos desde el archivo CSV
                Console.WriteLine("Datos cargados desde el archivo CSV.");
                var dataView = _mlContext.Data.LoadFromTextFile<WordCorrection>(
                    path: ExcelPath,
                    hasHeader: true,
                    separatorChar: ';');

                // Entrenar el modelo
                Console.WriteLine("Entrenando el  modelo...");
                var model = context.Fit(dataView);

                // Save the trained model
                _mlContext.Model.Save(model, dataView.Schema, modelPath);

                Console.WriteLine($"Modelo guardado en: {modelPath}");

                // INIT Prediction Engine 
                _predictor = _mlContext.Model.CreatePredictionEngine<WordCorrection, CorrectWordPrediction>(model);

            }

        }


        public async Task<List<string>> Predict(string pMisspelledWord)
        {
            List<string> output = new List<string>();

            string[] array = pMisspelledWord.Split(' ');

            if (array.Length == 1)
            {
                string misspelledWord = array.Length > 0 ? array[0] : pMisspelledWord;

                output.AddRange(await PredictListWords(misspelledWord));

                return output;
            }
            else
            {
                string line = string.Empty;

                for (int i = 0; i < array.Length; i++)
                {
                    line += await PredictWord(array[i]) + " ";
                }

                output.Add(line);
            }

            return output;
        }

        public async Task<string> PredictWord(string misspelledWord)
        {
            string output = string.Empty;

            int size = 4; // ASPI

            // OMITIR CODIGOS DE BARRAS Y NUMEROS 
            string strNum= misspelledWord;
            if (misspelledWord.ToUpper().StartsWith("P"))
            {
                strNum = misspelledWord.ToUpper().Replace("P", "").Replace("A", "");                
            }
            long num;
            bool isNum = long.TryParse(strNum, out num);
            if (isNum)
            {
                return output;
            }

            // Busca si la palabra COMPLETA hace match en la DB
            var lstMatch = await GetPalabras(misspelledWord);
            if (lstMatch.Count == 1)
            {
                output = lstMatch.FirstOrDefault();
            }
            else // Predecir la palabra
            {
                if (misspelledWord.Length < size) // SE REQUIEREN AL MENOS 4 LETRAS 
                {
                    output = misspelledWord;
                    return output;
                }
                else // Predicir la palabra con base al entrenamiento
                {
                    var input = new WordCorrection { MisspelledWord = misspelledWord };
                    var prediction = _predictor.Predict(input);

                    output = prediction.PredictedWord;
                }
            }

            string jsonString = JsonSerializer.Serialize(output);

            string res = await GuardarPalabra(misspelledWord, jsonString); // DB FARMA_APP TBL TopPalabras

            return output;
        }


        public async Task<List<string>> PredictListWords(string misspelledWord)
        {
            List<string> lstOutput = new List<string>();
            List<string> lstMatch = new List<string>();

            int size = 4; // ASPI

            // OMITIR CODIGOS DE BARRAS Y NUMEROS 
            string strNum= misspelledWord;
            if (misspelledWord.ToUpper().StartsWith("P"))
            {
                strNum = misspelledWord.ToUpper().Replace("P", "").Replace("A", "");               
            }
            long num;
            bool isNum = long.TryParse(strNum, out num);
            if (isNum)
            {
                return lstOutput;
            }

            // Busca si la palabra completa hce match en la DB
            var match = await GetPalabras(misspelledWord);
            if (match.Count == 1)
            {
                lstOutput.AddRange(match);
            }
            else // Predecir la palabra
            {
                if (misspelledWord.Length < size) // SE REQUIEREN AL MENOS 4 LETRAS 
                {
                    // output.Add(misspelledWord); // OMITIR
                    return lstOutput;
                }
                else // Predicir la palabra con base al entrenamiento
                {
                    var input = new WordCorrection { MisspelledWord = misspelledWord };
                    var prediction = _predictor.Predict(input);

                    // Si la palabra no está CONTENIDA en la prediccion
                    if (!prediction.PredictedWord.ToUpper().Contains(misspelledWord.ToUpper()))
                    {
                        // Buscar si la palabra está CONTENIDA %% en la base de datos
                        lstMatch = await GetPalabras($"%{misspelledWord}%");
                        lstOutput.AddRange(lstMatch);
                    }

                    // Si no hay match en la db de la palabra ingresada
                    if (lstOutput.Count == 0)
                    {
                        lstMatch = await GetPalabras($"%{prediction.PredictedWord}%");
                        lstOutput.AddRange(lstMatch);
                    }

                    // Por ultimo
                    // SE AGREGA LA PREDICCION PARA TENER CONTROL
                    // SI SE AGREGA O SI YA FUE AGREGADA CUANDO SE BUSCO EL MATCH EN LA DB
                    if (!lstOutput.Contains(prediction.PredictedWord))
                    {
                        lstOutput.Insert(0, prediction.PredictedWord);
                    }

                    lstOutput.AddRange(lstMatch);
                }
            }

            var output = lstOutput.Distinct().ToList(); // Quitar duplicados

            string jsonString = JsonSerializer.Serialize(output);

            string res = await GuardarPalabra(misspelledWord, jsonString); // DB FARMA_APP TBL TopPalabras

            return output;
        }

        /// <summary>
        /// Busca en la base de datos
        /// </summary>
        /// <param name="word"></param>
        /// <returns></returns>
        public async Task<List<string>> GetPalabras(string word)
        {
            List<string> list = new List<string>();

            SqlConnection conn = new SqlConnection(stringConnection1);
            try
            {
                conn.Open();

                //Consulta a base de datos 1
                string queryString = $"SP_GetPalabras";

                DataTable dt1 = new DataTable();

                SqlCommand cmd = new SqlCommand(queryString, conn);
                if (cmd != null)
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@str", word);

                    SqlDataAdapter da = new SqlDataAdapter(cmd);
                    da.Fill(dt1);

                    foreach (DataRow row in dt1.Rows)
                    {
                        string str = row.ItemArray[0].ToString();
                        list.Add(str);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                conn.Close();
            }

            return list;
        }


        public async Task<string> GuardarPalabra(string word, string output)
        {
            List<string> list = new List<string>();

            SqlConnection conn = new SqlConnection(stringConnection1);
            try
            {
                int count;

                conn.Open();

                // Inserta o actualiza el contador de palabras 
                string queryString = $"FARMA_APP.dbo.SP_GuardarPalabra";

                using (SqlCommand cmd = new SqlCommand(queryString, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@word", word);
                    cmd.Parameters.AddWithValue("@output", output);

                    // Devuelve el numero de filas afectas 
                    object? result = await cmd.ExecuteNonQueryAsync();

                    int.TryParse(result?.ToString(), out count);
                }

                return (count > 1 ? "OK" : "GuardarPalabras> Error en el stored procedure.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return $"GuardarPalabras> {ex.Message}";
            }
            finally
            {
                conn.Close();
            }
        }

    }

}
