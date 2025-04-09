using Microsoft.AspNetCore.Mvc;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;


using Microsoft.Data.SqlClient;
using System;
using System.Data;

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
		private readonly string stringConnection2;

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
			bool entrernar = false;
			bool zip = true;
			bool excel = false;

            if (entrernar)
            {
                // Configurar el Data Base Loader
                var loader = _mlContext.Data.CreateDatabaseLoader<WordCorrection>();

                // Connection String
                //var connectionString = "Server=192.200.9.131;Database=DB_GENESIS_CENTRAL; User Id=sa; Password=bofasa1$; Encrypt=True; TrustServerCertificate=True;";

                var sqlCommand = "SELECT CorrectWord, MisspelledWord  FROM [DB_GENESIS_CENTRAL].[dbo].ML_Productos WHERE ISNULL(Estado,'A') = 'A' ";

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

        public async Task<List<string>> Predict(string misspelledWord)
		{            
			//Aun esta en desarrollo y requeriere una nueva tabla que solo tengo localhost 
            //GuardarPalabra(misspelledWord); //await GuardarPalabra(misspelledWord); el await genera error extraño

            var input = new WordCorrection { MisspelledWord = misspelledWord };

			var prediction = _predictor.Predict(input);


			//// Realizar las predicciones
			//List<MyPrediction> predicciones = new List<MyPrediction>();
			//foreach (var dato in datosEntrada)
			//{
			//    var prediction = predictionEngine.Predict(dato);
			//    predicciones.Add(prediction);
			//}


			//// Imprimir o utilizar las predicciones
			//foreach (var prediccion in _predictor)
			//{
			//    Console.WriteLine($"Predicción: {prediccion.PredictedLabel}");
			//}

			List<string> lst = new List<string>();
			int size = 4; // aspi
			string bitWord = misspelledWord;
			if (misspelledWord.Length >= size)
			{
				bitWord = misspelledWord.Substring(0, size);
			}

			var res = await GetPalabras(bitWord);
			lst.AddRange(res);

			if (res.Count == 0)
			{
				res = await GetPalabras(prediction.PredictedWord);
				lst.AddRange(res);
			}

			if (!lst.Contains(prediction.PredictedWord))
			{
				lst.Add(prediction.PredictedWord);
			}

			return lst;
		}




		public async Task<List<string>> GetPalabras(string word)
		{
			List<string> list = new List<string>();
			SqlConnection conn = new SqlConnection(stringConnection1);
			try
			{
				// Database=DB_GENESIS_CENTRAL;                 

				conn.Open();

				//Consulta a base de datos 1
				string queryString = $"SP_GetPalabras";

				SqlCommand cmd2;

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

		public async void GuardarPalabra(string word)
		{
			SqlConnection conn = new SqlConnection(stringConnection2);
			try
			{				
				conn.Open();

                // Consulta a base de datos 1
                // cambiar a DB_GENESIS_CENTRAL;                 
                string queryString = $"Select isnull(sum(Cantidad),0) Cantidad from db.dbo.Corrector where palabra = '{word}'; ";

				SqlCommand cmd2;

				using (SqlCommand cmd = new SqlCommand(queryString, conn))
				{
					object? result = await cmd.ExecuteScalarAsync();

					int count;

					int.TryParse(result?.ToString(), out count);

					if (count == 0)
					{
						queryString = $"Insert into db.dbo.Corrector values( '{word}', 1 )";
						cmd2 = new SqlCommand(queryString, conn);
					}
					else
					{
						queryString = $"Update db.dbo.Corrector set Cantidad = {count + 1} where palabra = '{word}'; ";
						cmd2 = new SqlCommand(queryString, conn);
					}

					result = await cmd2.ExecuteScalarAsync();
				}

				//return true;
				return;
            }
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);

				//return false;
				return;
			}
			finally
			{
				conn.Close();
			}
		}
	}

}
