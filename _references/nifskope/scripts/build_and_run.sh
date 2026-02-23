echo "Running qmake6..."
qmake6 NifSkope.pro

echo "Building project..."
make -j8

echo "Running executable..."
./release/NifSkope.exe
