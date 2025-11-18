#!/bin/bash
sudo apt-get install -i wget
wget https://github.com/libjpeg-turbo/libjpeg-turbo/releases/download/3.0.3/libjpeg-turbo-official_3.0.3_arm64.deb
sudo dpkg -i libjpeg-turbo-official_3.0.3_arm64.deb

