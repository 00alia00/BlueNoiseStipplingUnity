/* 
	================================================================================
	Copyright (c) 2012, Jose Esteve. http://www.joesfer.com
	This software is released under the LGPL-3.0 license: http://www.opensource.org/licenses/lgpl-3.0.html	
	================================================================================

    Changes to support point lists by Alia McCutcheon 2020
*/

using System;

namespace BlueNoise
{
    public class API
    {
        WangTileSet tile_set;

        public WangTileSet GetTileSet()
        {
            return tile_set;
        }

        public WangTileSet GenerateTiles(int numColors, int samplesPerTile, int numThreads)
        {
            tile_set = new WangTileSet();
            tile_set.Generate(numColors, samplesPerTile, numThreads);

            return tile_set;
        }

        public PointSet Stipple(float density, float tone_scale_bias, int width, int height, int random_start_tile_index)
        {
            int tonalRange = (int)System.Math.Pow(10, (int)(tone_scale_bias * 6)) + 10000; // this is merely a heuristic trying to produce sensible values for the tonalRange variable from a [0,1] source range

            PointSet result = new PointSet(width, height);
            WTStipple stipple = new WTStipple(density, random_start_tile_index, tile_set, tonalRange, result);

            return result;
        }

        public WangTileSet LoadTilesXML(string tile_file_name)
        {
            tile_set = WangTileSet.FromFile(tile_file_name);
            return tile_set;
        }

        public void SaveTilesXML(string tile_file_name)
        {
            tile_set.Serialize(tile_file_name);
        }

        public WangTileSet LoadTilesBinary(string tile_file_name)
        {
            tile_set = WangTileSet.FromBinaryFile(tile_file_name);
            return tile_set;
        }

        public void SaveTilesBinary(string tile_file_name)
        {
            tile_set.SerializeBinary(tile_file_name);
        }

        public PointSet StippleFromFile(float density, float tone_scale_bias, int width, int height, string tile_path, int random_start_tile_index)
        {
            WangTileSet wts = WangTileSet.FromFile(tile_path);

            int tonalRange = (int)System.Math.Pow(10, (int)(tone_scale_bias * 6)) + 10000; // this is merely a heuristic trying to produce sensible values for the tonalRange variable from a [0,1] source range

            PointSet result = new PointSet(width, height);
            WTStipple stipple = new WTStipple(density, random_start_tile_index, wts, tonalRange, result);

            return result;
        }
    }


}