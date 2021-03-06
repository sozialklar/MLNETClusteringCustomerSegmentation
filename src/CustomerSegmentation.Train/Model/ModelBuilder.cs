﻿using System;
using CustomerSegmentation.RetailData;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.Transforms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using static CustomerSegmentation.Model.ModelHelpers;

namespace CustomerSegmentation.Model
{
    public class ModelBuilder
    {
        private readonly string transactionsDataLocation;
        private readonly string offersDataLocation;
        private readonly string modelLocation;

        public ModelBuilder(string transactionsDataLocation, string offersDataLocation, string modelLocation)
        {
            this.transactionsDataLocation = transactionsDataLocation;
            this.offersDataLocation = offersDataLocation;
            this.modelLocation = modelLocation;
        }

        public async Task BuildAndTrain(int kClusters = 4)
        {
            var preProcessData = DataHelpers.PreProcess(offersDataLocation, transactionsDataLocation);

            var learningPipeline = BuildModel(preProcessData, kClusters);

            PredictionModel<PivotData, ClusteringPrediction> model = TrainModel(learningPipeline);

            if (!string.IsNullOrEmpty(modelLocation))
            {
                await SaveModel(model);
            }
        }

        private async Task SaveModel(PredictionModel<PivotData, ClusteringPrediction> model)
        {
            ConsoleWriteHeader("Save model to local file");
            DeleteAssets(modelLocation);
            await model.WriteAsync(modelLocation);
            Console.WriteLine($"Model saved: {modelLocation}");
        }

        protected PredictionModel<PivotData, ClusteringPrediction> TrainModel(LearningPipeline pipeline)
        {
            ConsoleWriteHeader("Training customer segmentation model");
            return pipeline.Train<PivotData, ClusteringPrediction>();
        }

        protected LearningPipeline BuildModel(IEnumerable<PivotData> pivotData, int kClusters)
        {
            ConsoleWriteHeader("Build model pipeline");

            var pipeline = new LearningPipeline();

            // The CollectionDataSource class allows to use a regular in memory dataset 
            // as input for the learning pipeline
            pipeline.Add(CollectionDataSource.Create(pivotData));

            // All dataset columns must be combined in a single column
            var columnsNumerical = ModelHelpers.ColumnsNumerical<PivotData>();
            pipeline.Add(new ColumnConcatenator(outputColumn: "Features", columnsNumerical));

            // Add a couple of columns derived from the PCA. 
            // These columns will be used for translating a multidimensional sample 
            // to two-dimension sample which can be easily plotted in 2D 
            // http://ufldl.stanford.edu/wiki/index.php/PCA
            pipeline.Add(new PcaCalculator(("Features", "PCAFeatures")) { Rank = 2, Seed = 42 });

            //pipeline.Add(new ColumnConcatenator("Features", "NumericalFeatures", "PCAFeatures"));

            // The Learner is the last element in the pipeline. In this case, we use a k-Means algorithm
            // that is able to do unsupervised learning. The output of this learner will be a model 
            // that will classify samples in different categories. One drawback from this algorithm is that
            // you need to set up the total number of clusters. In a real case, you will need to test this 
            // hyperparameter and check how fits your own dataset
            pipeline.Add(new KMeansPlusPlusClusterer() { K = kClusters });

            return pipeline;
        }
    }
}
