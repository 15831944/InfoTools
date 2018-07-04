
function printOutPositions(row, table, transform) {
    while (row = table.Next()) {
        var geom = row.GEOMETRY;
        if (row.NAME != null && geom.Type == 1) {

            var bbox3d = geom.BBox3d;
            bbox3d = transform.transformBBox3d(bbox3d)

            var rotation = row.MODEL_ROTATE_Z;
            if (rotation == null) {
                rotation = 0.0;
            }

            print(
                "\t\t" +
                "<ObjectPosition Name=\"" + row.NAME + "\""
                + " X=\"" + bbox3d.Center.X + "\" Y=\"" + bbox3d.Center.Y + "\" Z=\"" + bbox3d.Max.Z + "\""
                + " Z_Rotation=\"" + rotation + "\" />");

        }
    }
}



function main() {

    var model = app.ActiveModel;

    var dbCoordSys = model.CoordSysWkt;

    var userCoordSys = model.UserCoordSysWkt;

    var transform = new adsk.SRSTransform(dbCoordSys, userCoordSys);


    var modelDb = app.ActiveModelDb;
    var filter1 = "";
    var row;


    var tableBARRIERS = modelDb.Table("BARRIERS");
    tableBARRIERS.StartQuery(filter1);
    print("BARRIERS");
    print("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    print("<PositionData>");
    print("\t<ObjectPositions>");
    printOutPositions(row, tableBARRIERS, transform);
    tableBARRIERS.EndQuery();
    print("\t</ObjectPositions>");
    print("</PositionData>");

    print("");
    print("");
    print("");

    var tableCITY_FURNITURE = modelDb.Table("CITY_FURNITURE");
    tableCITY_FURNITURE.StartQuery(filter1);
    print("CITY_FURNITURE");
    print("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    print("<PositionData>");
    print("\t<ObjectPositions>");
    printOutPositions(row, tableCITY_FURNITURE, transform);
    tableCITY_FURNITURE.EndQuery();
    print("\t</ObjectPositions>");
    print("</PositionData>");

}

main();
gc();
