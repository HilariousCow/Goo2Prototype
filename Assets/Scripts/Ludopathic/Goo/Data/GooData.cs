using System.Collections.Generic;

namespace Ludopathic.Goo.Data
{
    struct GooData : ITeamID
    {
        public int TeamID { get; set; }

        private List<BlobData> ListOfBlobs;//Compiled by a job which finds all blobs of the same type. Perhaps this is a list of indices in a master list instead.
        private List<GooGroupData> ListOfGooGroups;//Compiled by a job which floodfills blobs in the same team
    }
}